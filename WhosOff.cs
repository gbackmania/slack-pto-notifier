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
                var ptoMembers = members
                   ?.Where(e => e.Value<bool>("deleted") == false)
                   ?.Where(e => e.Value<bool>("is_bot") == false)
                   ?.Where(e => ((string)e.Value<dynamic>("profile")?.Value<string>("status_text")).HasAnyKeywords(Constants.Keywords) || ((string)e.Value<dynamic>("profile")?.Value<string>("status_text")).HasAnyPhrases(Constants.Phrases));

                if (ptoMembers == null || !ptoMembers.Any()) return new OkObjectResult("No team member is currently off according to their status.".AddLineBreak(2) + Constants.BotAlgoDesc);

                string statusExpiresOn = null;
                List<string> lines = new List<string>();
                string displayName = null;
                DateTime utc;
                string until = null;
                foreach (JToken u in ptoMembers)
                {
                    statusExpiresOn = u.Value<dynamic>("profile")?.Value<string>("status_expiration") ?? null;
                    utc = Constants.Epoch.AddSeconds(Convert.ToInt64(statusExpiresOn));
                    until = statusExpiresOn != null && statusExpiresOn != "0" ? $"until `{utc} UTC`" : string.Empty;

                    displayName = u.Value<dynamic>("profile")?.Value<string>("display_name");
                    lines.Add($"• {displayName} {until}");
                }
                var message = "The following team members are currently off according to their status.".AddLineBreak(2) + lines.BuildMessage().AddLineBreak(2) + Constants.BotAlgoDesc;
                return new OkObjectResult(message);
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(@"Oopsie! Something went wrong. Please try `\whosoff` or `\whoisoff`command again.");
            }
        }
    }
}
