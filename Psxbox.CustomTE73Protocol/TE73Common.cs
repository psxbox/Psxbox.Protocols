using System.Collections.Frozen;

namespace Psxbox.CustomTE73Protocol
{
    public static class TE73Common
    {
        public static readonly FrozenDictionary<string, string> TAGS = new Dictionary<string, string>()
        {
            [""] = "",

        }.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
