using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace MoveTeamProject
{
    public static class Webapi
    {
        public static async Task<T> GetTfsObject<T>(string url)
        {
            try
            {
                using (var client = new HttpClient(new HttpClientHandler() { Credentials = CredentialCache.DefaultCredentials }))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json;api-version=1.0");
                    using (var response = client.GetAsync(url).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        var responseBody = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<T>(responseBody);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return default(T);
        }

        public static async Task<T> PatchTfsObject<T>(T type, string url)
        {
            try
            {
                using (var client = new HttpClient(new HttpClientHandler() { Credentials = CredentialCache.DefaultCredentials }))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json;api-version=1.0");
                    var content = JsonConvert.SerializeObject(type);
                    HttpContent dataContent = new StringContent(content, Encoding.UTF8, "application/json");
                    using (var response = client.PatchAsync(url, dataContent).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        var responseBody = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<T>(responseBody);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
            return default(T);
        }
    }
}