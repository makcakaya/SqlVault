namespace SqlVault
{
    public sealed partial class SqlVault
    {
        private static readonly string ParamContextKey = "@ContextKey";
        private static readonly string ParamElementKey = "@ElementKey";
        private static readonly string ParamValue = "@Value";
        private static readonly string SqlCreateTablePostgreSQL = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} TEXT, PRIMARY KEY({1}, {2}));";
        private static readonly string SqlCreateTableSQLServer = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} NVARCHAR(MAX), PRIMARY KEY({1}, {2}));";
        private static readonly string SqlCreateTableMySQL = "CREATE TABLE IF NOT EXISTS {0}({1} INTEGER, {2} INTEGER, {3} TEXT, PRIMARY KEY({1}, {2}));";
        private static readonly string SqlDropTable = "DROP TABLE IF EXISTS {0}";
        private static readonly string SqlSelectAllFormat = "SELECT * FROM {0}";
        private static readonly string SqlSelectFormat = "SELECT * FROM {0} WHERE {1}=" + ParamContextKey + " AND {2}=" + ParamElementKey;
        private static readonly string SqlInsertFormat = "INSERT INTO {0}({1}, {2}, {3}) VALUES(" + ParamContextKey + ", " + ParamElementKey + ", " + ParamValue + ")";
        private static readonly string SqlUpdateFormat = "UPDATE {0} SET {1}=" + ParamContextKey + ", {2}=" + ParamElementKey + ", {3}=" + ParamValue +
            " WHERE {1}=" + ParamContextKey + " AND {2}=" + ParamElementKey;
    }
}
