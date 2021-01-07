using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;
using System.Collections.Specialized;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace PTO
{
    public static class WhosOff
    {
        [FunctionName("WhosOff")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [ServiceBus("whosoff", Connection = "SERVICEBUS_CONNECTION_STRING", EntityType = EntityType.Queue)]ICollector<string> queueCollector,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                queueCollector.Add(requestBody);
                dynamic jsonResponse = null;
                using (var httpClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(@"https://slack.com/api/users.list"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Secrets.BotUserOAuthToken);

                    var userInfoResponse = await httpClient.SendAsync(request);
                    userInfoResponse.EnsureSuccessStatusCode();

                    var content = await userInfoResponse.Content.ReadAsStringAsync();
                    jsonResponse = JsonConvert.DeserializeObject(content);
                    //PrintHeaders(req, log);
                }
                JArray members = jsonResponse?.members;
                var activeMembers = members
                    ?.RemoveDeletedMembers()
                    ?.RemoveBots();
                var ptoMembers = activeMembers
                    ?.GetMembersWithPTOStatus()
                    ?.SortByRealName();

                if (ptoMembers == null || !ptoMembers.Any()) return new OkObjectResult("No team member is currently off according to their status.".AddLineBreak(2) + Constants.BotAlgoDesc);

                string invokedUser = GetInvokedUserId(requestBody);
                var (invokedUserTzOffset, invokedUserTzLabel) = activeMembers.GetTimeZoneOffsetAndLabel(invokedUser);

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
                var message = $"The following team members are currently off according to their status. All times refer to your _current slack timezone_ - `{invokedUserTzLabel}`".AddLineBreak(2) + lines.BuildMessage().AddLineBreak(2) + Constants.BotAlgoDesc;
                return new OkObjectResult(message);
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(@"Oopsie! Something went wrong. Please try `/whoisoff` command again.");
            }
        }

        private static string GetInvokedUserId(string requestBody)
        {
            string query = System.Web.HttpUtility.UrlDecode(requestBody);
            NameValueCollection result = System.Web.HttpUtility.ParseQueryString(query);
            return result["user_id"].Trim();
        }

        private static void PrintHeaders(HttpRequest req, ILogger log)
        {
            string h = null;
            foreach (var header in req.Headers)
            {
                string headerContent = string.Join(",", header.Value.ToArray());
                h += $"{header.Key}: {headerContent}{Environment.NewLine}";
            }
            log.LogInformation(h);
        }
    }
}