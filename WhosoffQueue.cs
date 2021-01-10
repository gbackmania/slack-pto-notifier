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
        private static (string channel, string invokedUser) messageTo = (null, null);
        private static ILogger log = null;
        [FunctionName("WhosoffQueue")]
        public static async Task Run([ServiceBusTrigger("whosoff", Connection = "SERVICEBUS_CONNECTION_STRING")] string myQueueItem, ILogger logger)
        {
            try
            {
                log = logger;
                string message = null;
                string requestBody = myQueueItem;

                dynamic jsonResponse = null;
                using (var httpClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(@"https://slack.com/api/users.list"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Secrets.BotUserOAuthToken);

                    var userInfoResponse = await httpClient.SendAsync(request);
                    userInfoResponse.EnsureSuccessStatusCode();

                    var content = await userInfoResponse.Content.ReadAsStringAsync();
                    jsonResponse = JsonConvert.DeserializeObject(content);
                }
                JArray members = jsonResponse?.members;
                var activeMembers = members
                    ?.RemoveDeletedMembers()
                    ?.RemoveBots();
                var ptoMembers = activeMembers
                    ?.GetMembersWithPTOStatus()
                    ?.SortByRealName();

                messageTo = GetChannelAndUserId(requestBody);

                if (ptoMembers == null || !ptoMembers.Any())
                {
                    message = "No team member is currently off according to their status.".AddLineBreak(2) + Constants.BotAlgoDesc;
                    await PostEphemeralMessage(message);
                    return;
                }

                var (invokedUserTzOffset, invokedUserTzLabel) = activeMembers.GetTimeZoneOffsetAndLabel(messageTo.invokedUser);

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
                log.LogError($"Error: {ex.StackTrace}");
                await PostEphemeralMessage(@"Oopsie! Something went wrong. Please try `/whoisoff` command again.");
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

            var response = await PostEphemeralMessageActual(message, messageTo.channel, messageTo.invokedUser);

            dynamic postEphemeralResponse = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
            if (postEphemeralResponse?.error == null) return;
            //channel and user are the same so we can send the message directly to the user
            await PostEphemeralMessageActual(message, messageTo.invokedUser, messageTo.invokedUser);
        }

        private static async Task<HttpResponseMessage> PostEphemeralMessageActual(string message, string channel, string user)
        {
            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/chat.postEphemeral"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Secrets.BotUserOAuthToken);

                var postBody = BuildPostEphemeralMessageBody(message, channel, user);

                request.Content = new StringContent(postBody, Encoding.UTF8, @"application/json");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return response;
            }
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
            log.LogInformation($"off the queue - {printableQuery}");
        }
    }
}