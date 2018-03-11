using System;

namespace SqlVault
{
    [Flags]
    public enum InitOption
    {
        None = 0,
        CreateTableIfNotExists = 2,
        DropTableIfExists = 4,
        LoadData = 8,
        Default = CreateTableIfNotExists | LoadData
    }
}