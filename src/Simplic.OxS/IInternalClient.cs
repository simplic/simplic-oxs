using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Client for sending network/cluster internal requests
    /// </summary>
    public interface IInternalClient : IDisposable
    {
        /// <summary>
        /// Send http get request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        Task<T?> Get<T>([NotNull] string service, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null);

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
        Task<T?> Post<T, O>([NotNull] string service, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null);

        /// <summary>
        /// Send http put request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <typeparam name="O">Input object type. Will be send as json in the http body.</typeparam>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="body">Object to put as json</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        Task<T?> Put<T, O>([NotNull] string service, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null);

        /// <summary>
        /// Send http delete request. Throws an exception if no success-code is returned from the given endpoint.
        /// </summary>
        /// <typeparam name="T">Type of the object that is expected to be returned from the web-api.</typeparam>
        /// <param name="service">The name of the service that will be called</param>
        /// <param name="controller">Controller name (e.g. auth, mail, ...)</param>
        /// <param name="action">Action name (e.g. get, search, ...)</param>
        /// <param name="parameter">Query parameter as dictionary (key-value)</param>
        /// <returns>Result object</returns>
        /// <exception cref="Exception"></exception>
        Task<T?> Delete<T>([NotNull] string service, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null);

        /// <summary>
        /// Gets or sets the internal request schema. Should be http by default to ensure only internal calls.
        /// </summary>
        string Scheme { get; set; }
    }
}
