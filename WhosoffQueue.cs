using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PTO
{
    public static class WhosoffQueue
    {
        private static Command _command = null;
        private static ILogger _log = null;
        [FunctionName("WhosoffQueue")]
        public static async Task Run([ServiceBusTrigger("whosoff", Connection = "SERVICEBUS_CONNECTION_STRING")] string queueItem, ILogger log)
        {
            try
            {
                _log = log;
                string message = null;
                string requestBody = queueItem;
                dynamic jsonResponse = null;

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(@"https://slack.com/api/users.list"));
                request.Headers.Authorization = Constants.BearerToken;

                var userInfoResponse = await Constants.HttpClient.SendAsync(request);
                userInfoResponse.EnsureSuccessStatusCode();

                var content = await userInfoResponse.Content.ReadAsStringAsync();
                jsonResponse = JsonConvert.DeserializeObject(content);

                JArray members = jsonResponse?.members;
                var activeMembers = members
                    ?.RemoveDeletedMembers()
                    ?.RemoveBots();
                var ptoMembers = activeMembers
                    ?.GetMembersWithPTOStatus()
                    ?.SortByRealName();

                _command = Command.Parse(requestBody);

                if (ptoMembers == null || !ptoMembers.Any())
                {
                    message = "No team member is currently off according to their status.".AddLineBreak(2) + Constants.BotAlgoDesc;
                    await PostEphemeralMessage(message);
                    return;
                }

                var (invokedUserTzOffset, invokedUserTzLabel) = activeMembers.GetTimeZoneOffsetAndLabel(_command.UserId);

                Int64 statusExpiresOn;
                List<string> lines = new List<string>();
                string realName = null;
                string until = null;
                DateTime utc;
                DateTime timeInInvokedUserTz;
                foreach (JToken u in ptoMembers)
                {
                    statusExpiresOn = u.Value<dynamic>("profile")?.Value<Int64>("status_expiration") ?? 0;
                    utc = Constants.Epoch.AddSeconds(statusExpiresOn);
                    timeInInvokedUserTz = utc.AddSeconds(invokedUserTzOffset);

                    until = statusExpiresOn != 0 ? $"until `{timeInInvokedUserTz.ToShortenedLongForm()}`" : string.Empty;

                    realName = u.Value<dynamic>("profile")?.Value<string>("real_name");
                    lines.Add($"• {realName} {until}");
                }
                message = $"The following team members are currently off according to their status. All times refer to your _current slack timezone_ - `{invokedUserTzLabel}`".AddLineBreak(2) + lines.BuildMessage().AddLineBreak(2) + Constants.BotAlgoDesc;
                await PostEphemeralMessage(message);
                return;
            }
            catch (System.Exception ex)
            {
                _log.LogError($"Error: {ex.StackTrace}");
                await PostEphemeralMessage(@"Oopsie! Something went wrong. Please try `/whoisoff` command again in a little bit.");
                return;
            }
        }

        private static (string channel, string user) GetChannelAndUserId(string requestBody)
        {
            string query = System.Web.HttpUtility.UrlDecode(requestBody);
            NameValueCollection result = System.Web.HttpUtility.ParseQueryString(query);
            return (result["channel_id"].Trim(), result["user_id"].Trim());
        }

        private static async Task PostEphemeralMessage(string message)
        {
            var response = await PostEphemeralMessageActual(message, _command.ChannelId, _command.UserId);

            dynamic postEphemeralResponse = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
            if (postEphemeralResponse?.error == null) return;
            //channel and user are the same so we can send the message directly to the user
            await PostEphemeralMessageActual(message, _command.UserId, _command.UserId);
        }

        private static async Task<HttpResponseMessage> PostEphemeralMessageActual(string message, string channel, string user)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/chat.postEphemeral"));
            request.Headers.Authorization = Constants.BearerToken;

            var postBody = BuildPostEphemeralMessageBody(message, channel, user);

            request.Content = new StringContent(postBody, Encoding.UTF8, @"application/json");
            var response = await Constants.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response;
        }
        private static string BuildPostEphemeralMessageBody(string message, string channel, string user)
        {
            var postBody = JsonConvert.SerializeObject(
                    new
                    {
                        channel = channel,
                        text = message,
                        user = user
                    },
                    Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
            return postBody;
        }

        private static void LogRequest(NameValueCollection coll)
        {
            string printableQuery = null;
            var parameters = coll.AllKeys.SelectMany(coll.GetValues, (k, v) => new { key = k, value = v });
            foreach (var p in parameters) printableQuery += $"{p.key}: {p.value}, ";
            _log.LogInformation($"off the queue - {printableQuery}");
        }
    }
}