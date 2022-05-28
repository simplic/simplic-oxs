using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Settings;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text;
using System.Web;

namespace Simplic.OxS.Server
{
    public class InternalClient : IInternalClient
    {
        private readonly HttpClient client;
        private readonly ILogger<InternalClient> logger;

        public InternalClient(IOptions<AuthSettings> settings, IRequestContext requestContext, ILogger<InternalClient> logger)
        {
            this.logger = logger;

            client = new HttpClient();

            // Set authorization header
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("i-api-key", settings.Value.InternalApiKey);

            // TODO: Use consts
            // Set context header
            if (requestContext.UserId != null)
                client.DefaultRequestHeaders.Add("UserId", $"{requestContext.UserId}");

            if (requestContext.TenantId != null)
                client.DefaultRequestHeaders.Add("TenantId", $"{requestContext.TenantId}");

            if (requestContext.CorrelationId != null)
                client.DefaultRequestHeaders.Add("X-Correlation-ID", $"{requestContext.CorrelationId}");
        }

        public virtual async Task<T?> Get<T>([NotNull] string host, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, controller, action, parameter);

            var result = await client.GetAsync(endpoint);

            if (!result.IsSuccessStatusCode)
            {
                var error = await GetErrorMessage("GET", endpoint, result);

                logger.LogError(error);

                throw new Exception(error);
            }

            if (result.Content == null)
                return default(T);

            return await result.Content.ReadFromJsonAsync<T>();
        }

        public virtual async Task<T?> Post<T, O>([NotNull] string host, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, controller, action, parameter);

            var result = await client.PostAsJsonAsync(endpoint, body);

            if (!result.IsSuccessStatusCode)
            {
                var error = await GetErrorMessage("POST", endpoint, result);

                logger.LogError(error);

                throw new Exception(error);
            }

            if (result.Content == null)
                return default(T);

            return await result.Content.ReadFromJsonAsync<T>();
        }

        public virtual async Task<T?> Put<T, O>([NotNull] string host, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, controller, action, parameter);
            var result = await client.PutAsJsonAsync(endpoint, body);

            if (!result.IsSuccessStatusCode)
            {
                var error = await GetErrorMessage("PUT", endpoint, result);

                logger.LogError(error);

                throw new Exception(error);
            }

            if (result.Content == null)
                return default(T);

            return await result.Content.ReadFromJsonAsync<T>();
        }
        public virtual async Task<T?> Delete<T>([NotNull] string host, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, controller, action, parameter);

            var result = await client.DeleteAsync(endpoint);

            if (!result.IsSuccessStatusCode)
            {
                var error = await GetErrorMessage("DELETE", endpoint, result);

                logger.LogError(error);

                throw new Exception(error);
            }

            if (result.Content == null)
                return default(T);

            return await result.Content.ReadFromJsonAsync<T>();
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        private async Task<string> GetErrorMessage(string method, string endpoint, HttpResponseMessage result)
        {
            return $"Internal client error. Endpoint: [{method}] {endpoint} / Status {result.StatusCode} / {await result.Content.ReadAsStringAsync()}";
        }

        private string GenerateParameter(IDictionary<string, string>? parameter)
        {
            if (parameter == null)
                return "";

            var parameterBuilder = new StringBuilder();

            foreach (var param in parameter)
            {
                if (parameterBuilder.Length == 0)
                    parameterBuilder.Append('?');
                else
                    parameterBuilder.Append('&');

                parameterBuilder.Append($"{param.Key}={HttpUtility.UrlEncode(param.Key)}");
            }

            return parameterBuilder.ToString();
        }

        private string BuildUrl(string host, string controller, string action, IDictionary<string, string>? parameter)
        {
            var builder = new StringBuilder();

            builder.Append($"{Scheme}://{host}/v1-0/api/interal");

            if (!string.IsNullOrWhiteSpace(controller))
                builder.Append($"/{controller}");
            
            if (!string.IsNullOrWhiteSpace(action))
                builder.Append($"/{action}");

            // Add parameter
            builder.Append(GenerateParameter(parameter));

            return builder.ToString();
        }

        public string Scheme { get; set; } = "http";
    }
}
