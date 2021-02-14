using System.Collections.Specialized;

namespace PTO 
{
    internal class Command
    {
        public string Token { get; private set; }
        public string TeamId { get; private set; }
        public string ChannelId { get; private set; }
        public string UserId { get; private set; }
        public string Username { get; private set; }
        public static Command Parse(string input)
        {
            if(string.IsNullOrEmpty(input)) return new Command();
            
            string query = System.Web.HttpUtility.UrlDecode(input);
            NameValueCollection result = System.Web.HttpUtility.ParseQueryString(query);
            return new Command()
            {
                Token = result["token"],
                TeamId = result["team_id"],
                ChannelId = result["channel_id"],
                UserId = result["user_id"],
                Username = result["user_name"]

            };
        }
    }
}