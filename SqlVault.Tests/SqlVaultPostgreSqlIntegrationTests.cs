using Npgsql;
using System;
using Xunit;

namespace SqlVault.Tests
{
    public sealed class SqlVaultPostgreSqlIntegrationTests
    {
        private static readonly string CONNECTION_STRING = "PostgreSQL connection string";
        private static readonly string TABLE_NAME = "sql_vault";

        [Fact]
        public void CanCreateTableLoadAndSave()
        {
            var contextKey = 13;
            var elementKey = 19;
            var now = DateTime.Now.Ticks.ToString();
            var vault = GetVault();
            vault.CreateTableIfNotExists().Wait();
            vault.Load().Wait();
            vault.Save(contextKey, elementKey, now).Wait();

            Assert.Equal(now, vault.ContextRecords[contextKey].Elements[elementKey]);
        }

        private SqlVault GetVault()
        {
            return new SqlVault(() => new NpgsqlConnection(CONNECTION_STRING), new SqlVaultConfig(DbServer.PostgreSQL, TABLE_NAME));
        }
    }
}