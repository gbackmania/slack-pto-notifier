using System;
using System.Collections.Generic;
namespace PTO
{
    public static class Constants
    {
        public static readonly List<string> Keywords = new List<string>() { "pto", "vacation", "ooo" };
        public static readonly List<string> Phrases = new List<string>() { "out sick" };
        public static readonly string BotUserOAuthToken = "xoxb-**";
        //Change this based on the app user/bot id
        public static readonly string PTONotifierUserId = "U**";
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly string BotAlgoDesc = @"_PTO Notifier looks for one of the keywords: *pto, vacation, ooo*; or a phrase: *out sick*, in the status._";
    }
}