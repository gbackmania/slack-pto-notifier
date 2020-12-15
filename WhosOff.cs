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

namespace PTO
{
    public static class WhosOff
    {
        [FunctionName("WhosOff")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                dynamic jsonResponse = null;
                using (var httpClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(@"https://slack.com/api/users.list"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Constants.BotUserOAuthToken);

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

                if (ptoMembers == null || !ptoMembers.Any()) return new OkObjectResult("No team member is currently off according to their status.".AddLineBreak(2) + Constants.BotAlgoDesc);

                string invokedUser = await GetInvokedUserId(req);
                var (invokedUserTzOffset, invokedUserTzLabel) = activeMembers.GetTimeZoneOffsetAndLabel(invokedUser);

                Int64 statusExpiresOn;
                List<string> lines = new List<string>();
                string realName = null;
                DateTime utc;
                DateTime timeInInvokedUserTz;
                string until = null;
                foreach (JToken u in ptoMembers)
                {
                    statusExpiresOn = u.Value<dynamic>("profile")?.Value<Int64>("status_expiration") ?? 0;
                    utc = Constants.Epoch.AddSeconds(statusExpiresOn);
                    timeInInvokedUserTz = utc.AddSeconds(invokedUserTzOffset);

                    until = statusExpiresOn != 0 ? $"until `{timeInInvokedUserTz} {invokedUserTzLabel}`" : string.Empty;

                    realName = u.Value<dynamic>("profile")?.Value<string>("real_name");
                    lines.Add($"• {realName} {until}");
                }
                var message = "The following team members are currently off according to their status.".AddLineBreak(2) + lines.BuildMessage().AddLineBreak(2) + Constants.BotAlgoDesc;
                return new OkObjectResult(message);
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(@"Oopsie! Something went wrong. Please try `\whoisoff`command again.");
            }
        }

        private async static Task<string> GetInvokedUserId(HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string query = System.Web.HttpUtility.UrlDecode(requestBody);
            NameValueCollection result = System.Web.HttpUtility.ParseQueryString(query);
            return result["user_id"].Trim();
        }
    }
}