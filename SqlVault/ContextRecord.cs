using System.Collections.Generic;

namespace SqlVault
{
    public sealed class ContextRecord
    {
        public IDictionary<int, string> Elements { get; private set; }

        public ContextRecord(IDictionary<int, string> elements = null)
        {
            Elements = elements != null ? new Dictionary<int, string>(elements) : new Dictionary<int, string>();
        }
    }
}