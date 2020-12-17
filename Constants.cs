using System;
using System.Collections.Generic;
using System.Linq;
namespace PTO
{
    public static class Constants
    {
        public static readonly List<string> Keywords = new List<string>() { "pto", "ooo", "vacation", "vacationing" };
        public static readonly List<string> Phrases = new List<string>() { "out sick", "out of office" };
        public static readonly List<string> PTOEmojis = new List<string> { ":pto:" };
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly string BotAlgoDesc = $"_PTO Notifier looks for one of the keywords: *{string.Join(", ", Constants.Keywords)}*; or a phrase: *{string.Join(", ", Constants.Phrases)}*, in the status._";
    }
}