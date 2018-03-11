using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;

namespace SqlVault
{
    public sealed partial class SqlVault
    {
        private readonly DbConnector _connector;
        private readonly SqlVaultConfig _config;
        private readonly object _contextRecordsLock = new object();
        private readonly IDictionary<int, ContextRecord> _contextRecords = new Dictionary<int, ContextRecord>();
        private readonly object _loadLock = new object();
        private SqlVaultLoadState _loadState = SqlVaultLoadState.NotLoaded;

        public SqlVaultLoadState LoadState
        {
            get
            {
                lock (_loadLock)
                {
                    return _loadState;
                }
            }
        }

        private IDictionary<int, ContextRecord> ContextRecords
        {
            get
            {
                lock (_contextRecordsLock)
                {
                    return new Dictionary<int, ContextRecord>(_contextRecords);
                }
            }
        }

        public SqlVault(DbConnector connector, SqlVaultConfig config)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task Initialize(InitOption option)
        {
            if (option.HasFlag(InitOption.DropTableIfExists))
            {
                using (var conn = _connector())
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = string.Format(SqlDropTable, _config.TableName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            if (option.HasFlag(InitOption.CreateTableIfNotExists))
            {
                await CreateTableIfNotExists();
            }

            if (option.HasFlag(InitOption.LoadData))
            {
                await Load();
            }
        }

        public async Task SaveValue(int contextKey, int elementKey, string value)
        {
            lock (_loadLock)
            {
                if (_loadState != SqlVaultLoadState.Loaded)
                {
                    throw new InvalidOperationException($"Vault is not in {SqlVaultLoadState.Loaded.ToString()} state.");
                }
            }

            using (var conn = _connector())
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

        public async Task SaveFromFile(int contextKey, int elementKey, string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var value = await reader.ReadToEndAsync();
                await SaveValue(contextKey, elementKey, value);
            }
        }

        public bool TryGetValue(int contextKey, int elementKey, out string value)
        {
            ContextRecord context = null;
            if (ContextRecords.TryGetValue(contextKey, out context))
            {
                if (context.Elements.TryGetValue(elementKey, out value))
                {
                    return true;
                }
            }
            value = null;
            return false;
        }

        public string GetValue(int contextKey, int elementKey)
        {
            return ContextRecords[contextKey].Elements[elementKey];
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
            using (var conn = _connector())
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

        private async Task CreateTableIfNotExists()
        {
            using (var conn = _connector())
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

        private void AddParameter(string paramName, object value, DbCommand cmd)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = paramName;
            param.Value = value;
            cmd.Parameters.Add(param);
        }
    }
}