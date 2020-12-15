using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace PTO
{
    public static class Extensions
    {
        public static bool IsPTO(this string status)
        {
            return status.HasAnyKeywords(Constants.Keywords) || status.HasAnyPhrases(Constants.Phrases) || status.HasAnyKeywords(Constants.PTOEmojis);
        }
        public static bool HasAnyKeywords(this string status, List<string> keywords)
        {
            return status.Split(null)
                .Select(s => s.ToLowerInvariant())
                .Intersect(keywords)
                .Any();
        }

        public static bool HasAnyPhrases(this string status, List<string> phrases)
        {
            return phrases.Where(p => status.Trim().ToLowerInvariant().Contains(p)).Any();
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
        public static IEnumerable<JToken> RemoveDeletedMembers(this JArray members)
        {
            return members?.Where(e => e.Value<bool>("deleted") == false);
        }
        public static IEnumerable<JToken> RemoveBots(this IEnumerable<JToken> members)
        {
            return members?.Where(e => e.Value<bool>("is_bot") == false);
        }
        public static IEnumerable<JToken> GetMembersWithPTOStatus(this IEnumerable<JToken> members)
        {
            return members?.Where(e => ((string)e.Value<dynamic>("profile")?.Value<string>("status_text")).IsPTO() || ((string)e.Value<dynamic>("profile")?.Value<string>("status_emoji")).IsPTO());
        }
        public static IEnumerable<JToken> SortByRealName(this IEnumerable<JToken> members)
        {
            return members?.OrderBy(e => ((string)e.Value<dynamic>("profile")?.Value<string>("real_name_normalized")));
        }
        public static (int, string) GetTimeZoneOffsetAndLabel(this IEnumerable<JToken> members, string user)
        {
            var match = members?.First(e => e.Value<string>("id") == user);
            return (match?.Value<int>("tz_offset") ?? 0,
                    match?.Value<string>("tz_label") ?? string.Empty);
        }
    }
}