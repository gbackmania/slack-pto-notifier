using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace PTO
{
    public static class Extensions
    {
        public static bool HasAnyKeywords(this string status, List<string> keywords)
        {
            return status.Split(null)
                .Select(s => s.ToLowerInvariant())
                .Intersect(keywords)
                .Any();
        }

        public static bool HasAnyPhrases(this string status, List<string> phrases)
        {
            return phrases.Contains(status.Trim().ToLowerInvariant());
        }

        public static IEnumerable<JToken> GetDistinctUsers(this JArray innerElements)
        {
            return innerElements
                ?.Where(e => e.Value<string>("type") == "user")
                .GroupBy(u => u.Value<string>("user_id"))//group and select first item to remove duplicates
                .Select(x => x.First());
        }

        public static string BuildMessage(this IEnumerable<string> lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        public static string AddLineBreak(this string line, int linebreak)
        {
            switch (linebreak)
            {
                case 0: return line + string.Empty;
                case 1: return line + Environment.NewLine;
                case 2: return line + Environment.NewLine + Environment.NewLine;
                default: return line + string.Empty;
            }
        }
    }
}