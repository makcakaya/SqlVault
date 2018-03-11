using System.Collections.Generic;

namespace SqlVault
{
    internal sealed class ContextRecord
    {
        internal IDictionary<int, string> Elements { get; private set; } = new Dictionary<int, string>();
    }
}