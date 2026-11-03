using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Resolve;
using OxQL.Core.Engine;
using OxQL.Core.Models;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Says, once while the host starts, which services the model references have an
/// <c>InternalHosts</c> value that is not a host with an optional port
/// (<see cref="RemoteQueryClient.HostEntryForm"/>): one error line per service, naming the key,
/// what the value has that a host has not, and the form. The line never repeats the value. Such
/// a service is not configured and never called.
/// <para>
/// Where the query engine's check of the remote references stops the start (<c>Development</c>,
/// <c>Local</c> and continuous integration, unless <c>OxQL:Startup:StrictRemoteReferences</c>
/// says otherwise), this check stops it. The exception names how many services the lines are
/// about and the switch; the keys and the services are in the lines. It is registered ahead of
/// the engine's, which would name the same service as one without an entry.
/// </para>
/// </summary>
internal sealed class RemoteOwnerHostCheck(IServiceProvider provider, ILoggerFactory loggers) : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            Check();
            next(app);
        };

    /// <summary>Logs the findings and, on a strict host, throws them.</summary>
    /// <exception cref="InvalidOperationException">A strict host has a finding.</exception>
    internal void Check()
    {
        var logger = loggers.CreateLogger("Simplic.OxS.Server.OxQL");
        var findings = Findings(logger);

        foreach (var finding in findings)
            logger.LogError("{Finding}", finding);

        if (findings.Count > 0 && Strict())
        {
            throw new InvalidOperationException(
                $"OxQL refuses to start: {findings.Count} referenced {(findings.Count == 1 ? "service is" : "services are")} configured with a value that is no host; the error lines of the log name each. "
                + "To start without them, set OxQL:Startup:StrictRemoteReferences to false.");
        }
    }

    /// <summary>
    /// One sentence per referenced service whose host value is refused, ordinally by service.
    /// A host that replaced the client routes its owner calls itself and has none; neither has
    /// a host whose model cannot be read here, which its own checks report.
    /// </summary>
    private List<string> Findings(ILogger logger)
    {
        try
        {
            if (provider.GetService<IRemoteQueryClient>() is not RemoteQueryClient routes || provider.GetService<IEntityModelProvider>() is not { } model)
                return [];

            return [.. RemoteReferences.ServicesOf(model.Model)
                .Where(service => routes.RefusedHostOf(service) is not null)
                .Select(service => Describe(service, routes.RefusedHostOf(service)!))];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OxQL owner hosts could not be checked at start");

            return [];
        }
    }

    /// <summary>The sentence of one finding.</summary>
    internal static string Describe(string service, string defect) =>
        $"OxQL owner {service}: the value of {RemoteQueryClient.HostsSection}:{service} is not used, because {defect}. "
        + $"A value is {RemoteQueryClient.HostEntryForm}. "
        + $"Until it is one, the service is not configured on this host: no call is sent to it, and every reference into it resolves to nothing.";

    /// <summary>Whether a finding stops the host: what the query engine's check of the remote references decides by.</summary>
    private bool Strict()
    {
        var endpoint = provider.GetService<IOptions<OxQLEndpointOptions>>()?.Value;

        return RemoteReferenceCheck.IsStrict(
            endpoint?.StrictRemoteReferences,
            provider.GetService<OxQLOptions>()?.Startup.StrictRemoteReferences,
            provider.GetService<IHostEnvironment>()?.EnvironmentName,
            endpoint?.ContinuousIntegration ?? false);
    }
}
