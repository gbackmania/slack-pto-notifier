using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace PTO
{
    public static class Secrets
    {
        // Recommendation on reading app settings - https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library?tabs=v2%2Ccmd#environment-variables
        public static async Task<AuthenticationHeaderValue> GetBearerToken(string teamId)
        {
            var kvUri = Environment.GetEnvironmentVariable("Vault_URI");
            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            var token = await client.GetSecretAsync(teamId);
            return new AuthenticationHeaderValue("Bearer", token.Value.Value);
        }
        public static string GetClientId() => Environment.GetEnvironmentVariable("ClientId");
        public static string GetClientSecret() => Environment.GetEnvironmentVariable("ClientSecret");
        public static string GetVaultURI() => Environment.GetEnvironmentVariable("Vault_URI");
    }
}