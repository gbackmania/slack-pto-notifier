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
        public static SecretClient VaultClient => new SecretClient(new Uri(GetVaultURI()), new DefaultAzureCredential());
        public static async Task<string> GetSecret(string key)
        {
            var token = await VaultClient.GetSecretAsync(key);
            return token?.Value?.Value;
        }
    }
}