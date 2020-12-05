using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace PTO
{
    public static class PTONotifier
    {
        [FunctionName("PTONotifier")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                var sentByUser = data?.@event?.user;

                if(sentByUser == Constants.PTONotifierUserId) return new OkObjectResult("disregard messages by this app user/bot");

                JArray messageElements = data?.@event?.blocks?.First?.elements?.First?.elements;
                var channel = data?.@event?.channel;
                var thread = data?.@event?.thread_ts;

                var users = messageElements?.GetDistinctUsers();

                if (users == null || !users.Any()) return new OkObjectResult("no mentions");
                
                string message = string.Empty;
                List<string> lines = new List<string>();

                using (var httpClient = new HttpClient())
                {
                    dynamic jsonResponse = null;
                    foreach (JToken u in users)
                    {
                        var paramList = new Dictionary<string, string>()
                        {
                            {"user", u.Value<string>("user_id") }
                        };
                        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/users.info"))
                        {
                            Content = new FormUrlEncodedContent(paramList)
                        };
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Constants.BotUserOAuthToken);

                        var userInfoResponse = await httpClient.SendAsync(request);
                        userInfoResponse.EnsureSuccessStatusCode();

                        var content = await userInfoResponse.Content.ReadAsStringAsync();
                        jsonResponse = JsonConvert.DeserializeObject(content);
                        
                        var user = jsonResponse?.user;
                        string status = user?.profile?.status_text ?? string.Empty;
                        string statusExpiresOn = user?.profile?.status_expiration ?? "0";
                        string displayName = user?.profile?.display_name ?? "Guess who";//Should fall back to full_name...
                        var userTZ = user?.tz_label;

                        var utc = Constants.Epoch.AddSeconds(Convert.ToInt64(statusExpiresOn));
                        var userTZTime = utc.AddSeconds(Convert.ToInt64(user?.tz_offset));
                        
                        var backtick = "`";
                        string line = null;
                        
                        if (status.HasAnyKeywords(Constants.Keywords) || status.HasAnyPhrases(Constants.Phrases))
                        {
                            line = displayName + " seems to be off according to their status.";
                            if (!string.IsNullOrEmpty(statusExpiresOn) && statusExpiresOn != "0")
                            {
                                line = line.TrimEnd('.') + " until " + backtick + userTZTime + " " + userTZ + backtick + " or " + backtick + utc + " UTC." + backtick;
                            }
                            lines.Add(line);
                        }
                    }
                    message = lines.BuildMessage() + AdvertiseOtherFeatures();
                    //None of the @ mentions are on pto
                    if (string.IsNullOrEmpty(message)) 
                    {
                        log.LogInformation($"mentioned are not on pto");
                        return new OkObjectResult("mentioned are not on pto");
                    }

                    var chatRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/chat.postMessage"));
                    chatRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Constants.BotUserOAuthToken);

                    var fullMessage = "Hello there,".AddLineBreak(2) + message;
                    var postBody = JsonConvert.SerializeObject(
                        new //Anonymous type 
                        {
                            channel = channel,
                            text = fullMessage,
                            thread_ts = thread
                        },
                        Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });

                    chatRequest.Content = new StringContent(postBody, Encoding.UTF8, @"application/json");

                    var response = await httpClient.SendAsync(chatRequest);
                    response.EnsureSuccessStatusCode();
                    log.LogInformation($"pto message posted successfully.");
                }
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
            }
            return new OkObjectResult("pto message posted successfully.");
        }

        public static string AdvertiseOtherFeatures()
        {
            return $"{Environment.NewLine}{Environment.NewLine}_By the way, to see which team members are off, type `/whoisoff` in slack message box, aka command line._";
        }
    }
}
