using System;
using System.Text.RegularExpressions;

namespace LSCode.Core
{
    // Also compiled into the .NET Framework trace bridge.
    public static class ProfilerDatabaseFilter
    {
        public static string Prefix(string database)
        {
            var prefix = Regex.Replace(database, @"(?:_?(?:App|Sys)|_[AS])$", "", RegexOptions.IgnoreCase);
            return prefix.Length == 0 ? database : prefix;
        }

        public static bool Matches(string database, string prefix)
        {
            return prefix.Length == 0 || database.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
