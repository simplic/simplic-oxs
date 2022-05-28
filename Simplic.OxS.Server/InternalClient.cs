using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Settings;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server
{
    public class InternalClient : IDisposable
    {
        private HttpClient client;

        public InternalClient(IOptions<AuthSettings> settings, IRequestContext requestContext)
        {
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

        public async Task<T> Get<T>([NotNull] string endpoint, IDictionary<string, string> parameter = null)
        {
            client.BaseAddress = new Uri(endpoint);

            return default(T);
        }

        public async Task<T> Post<T, O>([NotNull] string endpoint, O body, IDictionary<string, string> parameter = null)
        {
            return default(T);
        }

        public async Task<T> Put<T, O>([NotNull] string endpoint, O body, IDictionary<string, string> parameter = null)
        {
            return default(T);
        }
        public async Task<T> Delete<T>([NotNull] string endpoint, IDictionary<string, string> parameter = null)
        {
            return default(T);
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        public string Scheme { get; set; } = "http";
    }
}
