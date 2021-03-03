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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if(data?.@event?.type == "app_uninstalled")
                {
                    log.LogInformation("func={func}, action={action}, team={team}", nameof(PTONotifier), ActionType.AppUninstalled, (string)data?.team_id);
                    return new OkObjectResult(ActionType.AppUninstalled);
                }

                //Verify request URL by responding to one-time initial challenge during slack app creation. dynamic type makes this a cake in the park ❤️
                if (data?.challenge != null) return new OkObjectResult(data?.challenge);
                
                string team = data?.@event?.team;
                string channel = data?.@event?.channel;
                string sentUser = data?.@event?.user;
                string thread = data?.@event?.thread_ts;

                log.LogInformation("func={func}, action={action}, team={team}, channel={channel}, user={user}", nameof(PTONotifier), ActionType.Received, team, channel, sentUser);

                if (data?.@event?.bot_id != null) return new OkObjectResult(ActionType.Bots);

                JArray messageElements = data?.@event?.blocks?.First?.elements?.First?.elements;
                if (messageElements == null)
                {
                    log.LogInformation("func={func}, action={action}, team={team}, channel={channel}, user={user}", nameof(PTONotifier), ActionType.MessageElementsBlockNotFound, team, channel, sentUser);
                    return new OkObjectResult(ActionType.MessageElementsBlockNotFound);
                } 
   
                var users = messageElements?.GetDistinctUsers();
                if (users == null || !users.Any()) return new OkObjectResult(ActionType.NoMentions);

                string message = string.Empty;
                var lines = new List<string>();
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
                    request.Headers.Authorization = await Secrets.GetBearerToken(team);
                    var userInfoResponse = await Constants.HttpClient.SendAsync(request);
                    userInfoResponse.EnsureSuccessStatusCode();

                    var content = await userInfoResponse.Content.ReadAsStringAsync();
                    jsonResponse = JsonConvert.DeserializeObject(content);

                    var user = jsonResponse?.user;
                    string status = user?.profile?.status_text ?? string.Empty;
                    string statusEmoji = user?.profile?.status_emoji ?? string.Empty;
                    string statusExpiresOn = user?.profile?.status_expiration ?? "0";
                    string displayName = user?.profile?.display_name ?? string.Empty;
                    string name = !string.IsNullOrEmpty(displayName)  ? displayName : user?.name;
                    string userTZ = user?.tz_label;

                    DateTime utc = Constants.Epoch.AddSeconds(Convert.ToInt64(statusExpiresOn));
                    DateTime userTZTime = utc.AddSeconds(Convert.ToInt64(user?.tz_offset));

                    string line = null;
                    if (status.IsPTO() || statusEmoji.IsPTO())
                    {
                        line = name + " seems to be off according to their status" + GetPTOUntilPhraseIfPresent(userTZ, userTZTime, utc);
                        lines.Add(line);
                    }
                }
                //None of the @ mentions are on pto
                if (lines == null || !lines.Any()) return new OkObjectResult(ActionType.MentionedNotPTO);

                message = lines.BuildMessage().AddLineBreak(2) + AdvertiseOtherFeatures();

                var chatRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/chat.postMessage"));
                chatRequest.Headers.Authorization = await Secrets.GetBearerToken(team);
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
                var response = await Constants.HttpClient.SendAsync(chatRequest);
                response.EnsureSuccessStatusCode();

                log.LogInformation("func={func}, action={action}, team={team}, channel={channel}, user={user}", nameof(PTONotifier), ActionType.PostMessage, team, channel, sentUser);
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(ex.StackTrace); //for testing purposes. slack disregards and doesn't expect anything from this hook. 
            }
            return new OkObjectResult(ActionType.PostMessage);
        }

        private static string AdvertiseOtherFeatures() => $"_By the way, to see which team members are off, type `/whoisoff` in slack message box, aka command line._";

        private static string GetPTOUntilPhraseIfPresent(string userTZLabel, DateTime userTZTime, DateTime utc)
        {
            if (utc == DateTime.UnixEpoch) return ".";
            return $" until `{userTZTime.ToShortenedLongForm()} {userTZLabel}` or `{utc.ToShortenedLongForm()} GMT.`";
        }

        private static class ActionType
        {
            public static readonly string Received = "recv";
            public static readonly string PostMessage = "postmsg";
            public static readonly string NoMentions = "nomentions";
            public static readonly string MentionedNotPTO = "mentionednotpto";
            public static readonly string Bots = "bots";
            public static readonly string MessageElementsBlockNotFound = "msgelemnotfound";
            public static readonly string AppUninstalled = "appuninstalled";
        }
    }
}
