using Google.Protobuf;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS
{
    /// <summary>
    /// Interface for calling external integrations in the same or another microservice
    /// </summary>
    public interface IRemoveServiceInvoker
    {
        /// <summary>
        /// Call external function
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <typeparam name="P">Parameter type</typeparam>
        /// <param name="functionName">Unique name of the function to call</param>
        /// <param name="parameter">Parameter that is required for function calling</param>
        /// <param name="functionName">Uri to the defaut target: [grpc] https://...  [http.post] https://</param>
        /// <returns>Processed value</returns>
        Task<T?> Call<T, P>([NotNull] string functionName, P parameter, string? defaultTargetUri)
            where T : class, IMessage<T>, new()
            where P : class, IMessage<P>, new();

        /// <summary>
        /// Call external function
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <typeparam name="P">Parameter type</typeparam>
        /// <param name="functionName">Unique name of the function to call</param>
        /// <param name="parameter">Parameter that is required for function calling</param>
        /// <param name="defaultImpl">Default implementation</param>
        /// <returns>Processed value</returns>
        Task<T> Call<T, P>([NotNull] string functionName, P parameter, Func<T, P>? defaultImpl);
    }
}
