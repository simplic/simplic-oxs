namespace Simplic.OxS.InternalClient
{
    /// <summary>
    /// Exception thrown when the internal client has an error.
    /// </summary>
    public class InternalClientException : Exception
    {
        /// <summary>
        /// Initializes a new Internal Client Exception.
        /// </summary>
        /// <param name="method">The method type as string. E.g. "GET"</param>
        /// <param name="endpoint">The called endpoint.</param>
        /// <param name="result">The HttpResponseMessage.</param>
        public InternalClientException(string method, string endpoint, HttpResponseMessage result)
            :base(GetErrorMessage(method, endpoint, result))
        {
            Method = method;
            Endpoint = endpoint;
            Result = result;
        }

        /// <summary>
        /// Initializes a new Internal Client Exception.
        /// </summary>
        /// <param name="method">The method type as string. E.g. "GET"</param>
        /// <param name="endpoint">The called endpoint.</param>
        /// <param name="result">The HttpResponseMessage.</param>
        /// <param name="innerException">The inner exception.</param>
        public InternalClientException(string method, string endpoint, HttpResponseMessage result, Exception? innerException)
            : base(GetErrorMessage(method, endpoint, result), innerException)
        {
            Method = method;
            Endpoint = endpoint;
            Result = result;
        }

        /// <summary>
        /// Gets the http method that is called.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Gets or the called endpoint.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Gets the http response.
        /// </summary>
        public HttpResponseMessage Result { get; }

        private static string GetErrorMessage(string method, string endpoint, HttpResponseMessage result)
        {
            return $"Internal client error. Endpoint: [{method}] {endpoint} " +
                $"/ Status {result.StatusCode} " +
                $"/ {result.Content.ReadAsStringAsync().Result}";
        }
    }
}
