using System.Text;
using System.Text.Json;

namespace AssessmentPlatform.Common.Implementation
{
    public class HttpService
    {
        private readonly HttpClient _httpClient;

        public HttpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<T?> SendAsync<T>(HttpMethod method,string url,object? body = null, Dictionary<string, string>? headers = null)
        {
            // Add headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (_httpClient.DefaultRequestHeaders.Contains(header.Key))
                        _httpClient.DefaultRequestHeaders.Remove(header.Key);

                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            // Build request
            var request = new HttpRequestMessage(method, url);

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            // Execute
            var response = await _httpClient.SendAsync(request);

            // If status is success ✔
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = await response.Content.ReadFromJsonAsync<T>();
                    return result;
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }
    }
}
