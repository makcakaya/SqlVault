using System;

namespace SqlVault
{
    public sealed class SqlVaultConfig
    {
        public DbServer Server { get; }
        public string TableName { get; }
        public string ContextKeyColumn { get; } = "context_key";
        public string ElementKeyColumn { get; } = "element_key";
        public string ValueColumn { get; } = "value";

        public SqlVaultConfig(DbServer server, string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException(nameof(tableName));
            }

            Server = server;
            TableName = tableName;
        }

        public SqlVaultConfig(DbServer server, string tableName, string contextKeyColumn, string elementKeyColumn, string valueColumn) : this(server, tableName)
        {
            if (string.IsNullOrEmpty(contextKeyColumn)) { throw new ArgumentException(nameof(contextKeyColumn)); }
            if (string.IsNullOrEmpty(elementKeyColumn)) { throw new ArgumentException(nameof(elementKeyColumn)); }
            if (string.IsNullOrEmpty(valueColumn)) { throw new ArgumentException(nameof(valueColumn)); }

            Server = server;
            ContextKeyColumn = contextKeyColumn;
            ElementKeyColumn = elementKeyColumn;
            ValueColumn = valueColumn;
        }
    }
}
