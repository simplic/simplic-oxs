using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Settings;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text;
using System.Web;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Client for sending network/cluster internal requests
    /// </summary>
    public class InternalClient : IInternalClient
    {
        private readonly HttpClient client;
        private readonly ILogger<InternalClient> logger;

        /// <summary>
        /// Initialize http client
        /// </summary>
        /// <param name="settings">Authentication settings</param>
        /// <param name="requestContext">Current request context</param>
        /// <param name="logger">Logger instance</param>
        public InternalClient(IOptions<AuthSettings> settings, IRequestContext requestContext, ILogger<InternalClient> logger)
        {
            this.logger = logger;

            client = new HttpClient();

            // Set authorization header
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(Constants.HttpAuthorizationSchemeInternalKey, settings.Value.InternalApiKey);

            // Set context header
            if (requestContext.UserId != null)
                client.DefaultRequestHeaders.Add(Constants.HttpHeaderUserIdKey, $"{requestContext.UserId}");

            if (requestContext.TenantId != null)
                client.DefaultRequestHeaders.Add(Constants.HttpHeaderTenantIdKey, $"{requestContext.TenantId}");

            if (requestContext.CorrelationId != null)
                client.DefaultRequestHeaders.Add(Constants.HttpHeaderCorrelationIdKey, $"{requestContext.CorrelationId}");
        }

        /// <summary>
        /// Send http get request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
            /// <param name="host">Host name (localhost, dns, ip-address)</param>
            /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task<T?> Get<T>([NotNull] string host, [NotNull] string service, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, service, controller, action, parameter);

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

        /// <summary>
        /// Send http post request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <typeparam name="O">Input object type. Will be send as json in the http body.</typeparam>
        /// <param name="host">Host name (localhost, dns, ip-address)</param>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="body">Object to post as json</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task<T?> Post<T, O>([NotNull] string host, [NotNull] string service, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, service, controller, action, parameter);

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

        /// <summary>
        /// Send http put request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <typeparam name="O">Input object type. Will be send as json in the http body.</typeparam>
        /// <param name="host">Host name (localhost, dns, ip-address)</param>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="body">Object to put as json</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task<T?> Put<T, O>([NotNull] string host, [NotNull] string service, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, service, controller, action, parameter);
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

        /// <summary>
        /// Send http delete request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <param name="host">Host name (localhost, dns, ip-address)</param>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task<T?> Delete<T>([NotNull] string host, [NotNull] string service, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null)
        {
            var endpoint = BuildUrl(host, service, controller, action, parameter);

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

        /// <summary>
        /// Dispose http client <see cref="HttpClient"/>
        /// </summary>
        public void Dispose()
        {
            client?.Dispose();
        }

        /// <summary>
        /// Gets a formatted error message
        /// </summary>
        /// <param name="method">Http method</param>
        /// <param name="endpoint">Endpoint query (url)</param>
        /// <param name="result">Result as string</param>
        private async Task<string> GetErrorMessage(string method, string endpoint, HttpResponseMessage result)
        {
            return $"Internal client error. Endpoint: [{method}] {endpoint} / Status {result.StatusCode} / {await result.Content.ReadAsStringAsync()}";
        }

        /// <summary>
        /// Convert parameter to query-string
        /// </summary>
        /// <param name="parameter">Parameter as dictionary</param>
        /// <returns>Query-string (http encoded)</returns>
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

        /// <summary>
        /// Build complete url (query-string)
        /// </summary>
        /// <param name="host">Host name</param>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name</param>
        /// <param name="action">Action name</param>
        /// <param name="parameter">Parameter (dictionary will be converted to ?...=...&...=...</param>
        /// <returns>Url as string</returns>
        private string BuildUrl(string host, string service, string controller, string action, IDictionary<string, string>? parameter)
        {
            var builder = new StringBuilder();

            builder.Append($"{Scheme}://{host}/v1/{service}-api/internal");

            if (!string.IsNullOrWhiteSpace(controller))
                builder.Append($"/{controller}");

            if (!string.IsNullOrWhiteSpace(action))
                builder.Append($"/{action}");

            // Add parameter
            builder.Append(GenerateParameter(parameter));

            return builder.ToString();
        }

        /// <summary>
        /// Gets or sets the internal request schema. Should be http by default to ensure only internal calls.
        /// </summary>
        public string Scheme { get; set; } = "http";
    }
}
