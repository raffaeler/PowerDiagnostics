using System;
using System.Diagnostics;
using System.Net.Http;
//using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StressTestWebApp
{
    internal class TestWebAppClient
    {
        public TestWebAppClient(HttpClient client)
        {
            this.Client = client;
        }

        public HttpClient Client { get; }

        public async Task<bool> GetPage(string relativeAddress)
        {
            bool result;
            try
            {
                var response = await Client.GetAsync(relativeAddress);
                var content = await response.Content.ReadAsStringAsync();
                result = response.IsSuccessStatusCode;
            }
            catch (HttpRequestException err)
            {
                //Debug.WriteLine($"Failed with status {err.StatusCode}");
                result = false;
            }
            catch (Exception)
            {
                result = false;
            }


            return result;
        }

        /// <summary>
        /// Unless the server always fails, this call will always succeed
        /// because Polly retries for several time when it fails.
        /// </summary>
        public async Task<bool> Post(string relativeAddress, string data)
        {
            bool result;
            try
            {
                var response = await Client.PostAsync(relativeAddress,
                    new StringContent($"\"{data}\""));
                    //JsonContent.Create(data));
                result = response.IsSuccessStatusCode;
            }
            catch (HttpRequestException err)
            {
                //Debug.WriteLine($"Failed with status {err.StatusCode}");
                result = false;
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        }

    }
}