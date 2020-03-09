using System;
using System.Text.RegularExpressions;

namespace ExpirationScanner.Logic
{
    public static class WhitelistHelper
    {
        public static bool Matches(string name, string[] whitelist, bool ignoreCase = false)
        {
            var comp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            foreach (var entry in whitelist)
            {
                if (entry.Contains("*"))
                {
                    // only support * as regex pattern -> escape everything else
                    var regex = new Regex(Regex.Escape(entry).Replace("\\*", ".+"), options);
                    if (regex.IsMatch(name))
                        return true;
                }
                else
                {
                    if (name.Equals(entry, comp))
                        return true;
                }
            }
            return false;
        }
    }
}
