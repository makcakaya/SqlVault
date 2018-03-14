using System.Collections.Generic;

namespace SqlVault
{
    public interface IReadonlySqlVault
    {
        IDictionary<int, string> GetContextElements(int contextKey);

        string GetValue(int contextKey, int elementKey);

        bool TryGetValue(int contextKey, int elementKey, out string value);
    }
}