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
            [ServiceBus("whosoff", Connection = "SERVICEBUS_CONNECTION_STRING", EntityType = EntityType.Queue)] ICollector<string> queueCollector,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                queueCollector.Add(requestBody);
                return new OkObjectResult(string.Empty); //TODO: Send "is typing message"
            }
            catch (System.Exception ex)
            {
                log.LogError($"Error: {ex.StackTrace}");
                return new OkObjectResult(@"Oopsie! Something went wrong. Please try `/whoisoff` command again.");
            }
        }

        private static void LogHeaders(HttpRequest req, ILogger log)
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