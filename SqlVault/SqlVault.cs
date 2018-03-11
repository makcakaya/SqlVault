using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace SqlVault
{
    public sealed class SqlVault
    {
        private static readonly string ParamContextKey = "@ContextKey";
        private static readonly string ParamElementKey = "@ElementKey";
        private static readonly string ParamValue = "@Value";
        private static readonly string SqlCreateTablePostgreSQL = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} TEXT, PRIMARY KEY({1}, {2}));";
        private static readonly string SqlCreateTableSQLServer = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} NVARCHAR(MAX), PRIMARY KEY({1}, {2}));";
        private static readonly string SqlCreateTableMySQL = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} TEXT, PRIMARY KEY({1}, {2}));";
        private static readonly string SqlSelectAllFormat = "SELECT * FROM {0}";
        private static readonly string SqlSelectFormat = "SELECT * FROM {0} WHERE {1}=" + ParamContextKey + " AND {2}=" + ParamElementKey;
        private static readonly string SqlInsertFormat = "INSERT INTO {0}({1}, {2}, {3}) VALUES(" + ParamContextKey + ", " + ParamElementKey + ", " + ParamValue + ")";
        private static readonly string SqlUpdateFormat = "UPDATE {0} SET {1}=" + ParamContextKey + ", {2}=" + ParamElementKey + ", {3}=" + ParamValue +
            " WHERE {1}=" + ParamContextKey + " AND {2}=" + ParamElementKey;
        private readonly Func<DbConnection> _connectionFactory;
        private readonly SqlVaultConfig _config;
        private readonly object _contextRecordsLock = new object();
        private readonly IDictionary<int, ContextRecord> _contextRecords = new Dictionary<int, ContextRecord>();
        private readonly object _loadLock = new object();
        private SqlVaultLoadState _loadState = SqlVaultLoadState.NotLoaded;

        public IDictionary<int, ContextRecord> ContextRecords
        {
            get
            {
                lock (_contextRecordsLock)
                {
                    return new Dictionary<int, ContextRecord>(_contextRecords);
                }
            }
        }

        public SqlVault(Func<DbConnection> connectionFactory, SqlVaultConfig config)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task CreateTableIfNotExists()
        {
            using (var conn = _connectionFactory())
            {
                conn.Open();
                string commandText = null;
                switch (_config.Server)
                {
                    case DbServer.PostgreSQL:
                        commandText = SqlCreateTablePostgreSQL;
                        break;
                    case DbServer.SQLServer:
                        commandText = SqlCreateTableSQLServer;
                        break;
                    case DbServer.MySQL:
                        commandText = SqlCreateTableMySQL;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_config.Server));
                }
                var cmd = conn.CreateCommand();
                cmd.CommandText = string.Format(commandText, _config.TableName, _config.ContextKeyColumn, _config.ElementKeyColumn, _config.ValueColumn);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task Save(int contextKey, int elementKey, string value)
        {
            lock (_loadLock)
            {
                if (_loadState != SqlVaultLoadState.Loaded)
                {
                    throw new InvalidOperationException($"Vault is not in {SqlVaultLoadState.Loaded.ToString()} state.");
                }
            }

            using (var conn = _connectionFactory())
            {
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = string.Format(SqlSelectFormat, _config.TableName, _config.ContextKeyColumn, _config.ElementKeyColumn);
                AddParameter(ParamContextKey, contextKey, cmd);
                AddParameter(ParamElementKey, elementKey, cmd);
                AddParameter(ParamValue, value, cmd);

                var exists = false;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    exists = reader.HasRows;
                }

                cmd = conn.CreateCommand();
                if (exists)
                {
                    cmd.CommandText = string.Format(SqlUpdateFormat, _config.TableName, _config.ContextKeyColumn, _config.ElementKeyColumn, _config.ValueColumn);
                    AddParameter(ParamContextKey, contextKey, cmd);
                    AddParameter(ParamElementKey, elementKey, cmd);
                    AddParameter(ParamValue, value, cmd);
                    await cmd.ExecuteNonQueryAsync();

                    lock (_contextRecordsLock)
                    {
                        _contextRecords[contextKey].Elements[elementKey] = value;
                    }
                }
                else
                {
                    cmd.CommandText = string.Format(SqlInsertFormat, _config.TableName, _config.ContextKeyColumn, _config.ElementKeyColumn, _config.ValueColumn);
                    AddParameter(ParamContextKey, contextKey, cmd);
                    AddParameter(ParamElementKey, elementKey, cmd);
                    AddParameter(ParamValue, value, cmd);
                    await cmd.ExecuteNonQueryAsync();

                    ContextRecord context = null;
                    lock (_contextRecordsLock)
                    {
                        if (_contextRecords.TryGetValue(contextKey, out context))
                        {
                            context.Elements.Add(elementKey, value);
                        }
                        else
                        {
                            context = new ContextRecord();
                            context.Elements.Add(elementKey, value);
                            _contextRecords.Add(contextKey, context);
                        }
                    }
                }
            }
        }

        public async Task Load()
        {
            lock (_loadLock)
            {
                if (_loadState != SqlVaultLoadState.NotLoaded)
                {
                    throw new InvalidOperationException("SqlVault is either loading or already loaded.");
                }
                _loadState = SqlVaultLoadState.Loading;
            }

            try
            {
                await LoadInner();
                lock (_loadLock)
                {
                    _loadState = SqlVaultLoadState.Loaded;
                }
            }
            catch (DbException)
            {
                lock (_loadLock)
                {
                    _loadState = SqlVaultLoadState.DbError;
                }
                throw;
            }
        }

        private async Task LoadInner()
        {
            using (var conn = _connectionFactory())
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = string.Format(SqlSelectAllFormat, _config.TableName);
                var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var contextKey = (int)reader[_config.ContextKeyColumn];
                    var elementKey = (int)reader[_config.ElementKeyColumn];
                    var value = (string)reader[_config.ValueColumn];

                    ContextRecord record = null;
                    if (_contextRecords.TryGetValue(contextKey, out record))
                    {
                        record.Elements.Add(elementKey, value);
                    }
                    else
                    {
                        record = new ContextRecord();
                        record.Elements.Add(elementKey, value);
                        lock (_contextRecordsLock)
                        {
                            _contextRecords.Add(contextKey, record);
                        }
                    }
                }
            }
        }

        private void AddParameter(string paramName, object value, DbCommand cmd)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            param.Value = value;
            cmd.Parameters.Add(param);
        }
    }
}