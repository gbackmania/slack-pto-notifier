using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Collections.Generic;
using System.Net.Http;

namespace PTO
{
    public static class OAuthRedirect
    {
        [FunctionName("OAuthRedirect")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                var paramList = new Dictionary<string, string>()
                        {
                            {"code", req.Query["code"] },
                            {"client_id", await Secrets.GetClientId()},
                            {"client_secret", await Secrets.GetClientSecret()}
                        };
                var oauthreq = new HttpRequestMessage(HttpMethod.Post, new Uri(@"https://slack.com/api/oauth.v2.access"))
                {
                    Content = new FormUrlEncodedContent(paramList)
                };

                var oauthResponse = await Constants.HttpClient.SendAsync(oauthreq);
                oauthResponse.EnsureSuccessStatusCode();
                string oauthResponseBody = await oauthResponse.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(oauthResponseBody);
                string teamId = data?.team?.id;
                string accessToken = data?.access_token;
                string teamName = data?.team?.name;

                //save the code in vault and save the team details in mongo or cosmos
                var kvUri = Secrets.GetVaultURI();
                var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
                await client.SetSecretAsync(teamId, accessToken);

                return new OkObjectResult($"Successfully installed Out-Of-Office Slack app to {teamName} workspace");
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(string.Empty);
            }
        }
    }
}
