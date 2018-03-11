using Npgsql;
using System;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace SqlVault.Tests
{
    public sealed class SqlVaultPostgreSqlIntegrationTests
    {
        private static readonly string CONNECTION_STRING = "Host=testdb.cagmup7sak5q.eu-central-1.rds.amazonaws.com; User ID=mert; Password=password; Database=testdb";
        private static readonly string TABLE_NAME = "sql_vault";

        [Fact]
        public async Task CanInitializeDropTable()
        {
            var vault = GetVault();
            await vault.Initialize(InitOption.DropTableIfExists);

            await Assert.ThrowsAnyAsync<DbException>(() => vault.Load());
        }

        [Fact]
        public async Task CanInitializeCreateTableIfNotExists()
        {
            var vault = GetVault();
            await vault.Initialize(InitOption.CreateTableIfNotExists);
            await vault.Load();
        }

        [Fact]
        public async Task CanInitializeCreateTableAndLoad()
        {
            var vault = GetVault();
            await vault.Initialize(InitOption.CreateTableIfNotExists | InitOption.LoadData);

            Assert.Equal(SqlVaultLoadState.Loaded, vault.LoadState);
        }

        [Fact]
        public async Task CanCreateTableLoadAndSave()
        {
            var contextKey = 13;
            var elementKey = 19;
            var now = DateTime.Now.Ticks.ToString();
            var vault = GetVault();
            await vault.Initialize(InitOption.CreateTableIfNotExists);
            await vault.Load();
            await vault.SaveValue(contextKey, elementKey, now);

            Assert.Equal(now, vault.GetValue(contextKey, elementKey));
        }

        [Fact]
        public async Task CanSaveFromFile()
        {
            var vault = GetVault();
            await vault.Initialize(InitOption.Default);
            await vault.SaveFromFile(13, 19, "test_sql.sql");

            Assert.Equal("SELECT * FROM {0}", vault.GetValue(13, 19));
        }

        private SqlVault GetVault()
        {
            return new SqlVault(() => new NpgsqlConnection(CONNECTION_STRING),
                new SqlVaultConfig(DbServer.PostgreSQL, TABLE_NAME));
        }
    }
}