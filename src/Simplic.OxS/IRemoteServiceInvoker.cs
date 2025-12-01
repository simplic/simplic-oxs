using Google.Protobuf;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS
{
    /// <summary>
    /// Interface for calling external integrations in the same or another microservice
    /// </summary>
    public interface IRemoteServiceInvoker
    {
        /// <summary>
        /// Call external function
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <typeparam name="P">Parameter type</typeparam>
        /// <param name="contractOrUri">Uri to the defaut target: [grpc] https://...  [http.post] https://</param>
        /// <param name="parameter">Parameter that is required for function calling</param>
        /// <param name="defaultImpl">Default implementation when no contract was found</param>
        /// <returns>Processed value</returns>
        Task<T?> Call<T, P>([NotNull] string contractOrUri, P parameter, Func<P, Task<T>>? defaultImpl = null)
            where T : class, IMessage<T>, new()
            where P : class, IMessage<P>, new();
    }
}
