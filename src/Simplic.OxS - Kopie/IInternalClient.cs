using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server
{
    public interface IInternalClient : IDisposable
    {
        Task<T?> Get<T>([NotNull] string host, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null);

        Task<T?> Post<T, O>([NotNull] string host, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null);

        Task<T?> Put<T, O>([NotNull] string host, [NotNull] string controller, string action, O body, IDictionary<string, string>? parameter = null);

        Task<T?> Delete<T>([NotNull] string host, [NotNull] string controller, string action, IDictionary<string, string>? parameter = null);

        string Scheme { get; set; }
    }
}
