using System;
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
            var token = await GetSecret(teamId);
            return new AuthenticationHeaderValue("Bearer", token);
        }
        public static async Task<string> GetClientId() => await GetSecret("clientId");
        public static async Task<string> GetClientSecret() => await GetSecret("clientsecret");
        public static string GetVaultURI() => Environment.GetEnvironmentVariable("Vault_URI");
        public static async Task<string> GetSecret(string key)
        {
            var kvUri = GetVaultURI();
            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            var token = await client.GetSecretAsync(key);
            return token?.Value?.Value;
        }
    }
}