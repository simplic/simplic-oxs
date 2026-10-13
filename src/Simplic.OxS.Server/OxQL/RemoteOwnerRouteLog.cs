using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OxQL.Core.Engine;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Writes, once while the host starts, where the owner calls of every service the model
/// references go: the batch route and where its api version comes from. A wrong version segment
/// is a route the owner does not have, which at run time looks like an owner that is down; the
/// line shows it before the first query.
/// <para>
/// Only services with an <c>InternalHosts</c> entry are listed. A referenced service without one
/// is the query engine's own startup check, which names it.
/// </para>
/// </summary>
internal sealed class RemoteOwnerRouteLog(IServiceProvider provider, ILoggerFactory loggers) : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            Log();
            next(app);
        };

    /// <summary>
    /// Writes one line per configured service the model references remotely, ordinally sorted.
    /// The lines are information for an operator: whatever keeps them from being written is
    /// logged as a warning and never stops the start.
    /// </summary>
    internal void Log()
    {
        var logger = loggers.CreateLogger("Simplic.OxS.Server.OxQL");

        try
        {
            // A host that replaced the client routes its owner calls itself.
            if (provider.GetService<IRemoteQueryClient>() is RemoteQueryClient routes && provider.GetService<IEntityModelProvider>() is { } model)
                Log(logger, routes, RemoteReferences.ServicesOf(model.Model));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OxQL owner routes could not be listed at start");
        }
    }

    private static void Log(ILogger logger, RemoteQueryClient routes, IReadOnlyList<string> services)
    {
        foreach (var service in services)
        {
            if (routes.BatchUrl(service) is not { } route)
                continue;

            if (!Uri.TryCreate(route, UriKind.Absolute, out _))
            {
                logger.LogWarning(
                    "OxQL owner {Service}: its '{Section}' entry does not form a valid address ({Route}); the owner cannot be reached",
                    service,
                    RemoteQueryClient.HostsSection,
                    route);

                continue;
            }

            if (routes.ApiVersionSourceOf(service) is { } source && source != "default")
                logger.LogInformation("OxQL owner {Service}: {Route} (api version {ApiVersion} from {Source})", service, route, routes.ApiVersionOf(service), source);
            else
                logger.LogInformation(
                    "OxQL owner {Service}: {Route} (api version {ApiVersion}, the default: no {Section}:{Service} entry)",
                    service,
                    route,
                    routes.ApiVersionOf(service),
                    RemoteQueryClient.ApiVersionsSection,
                    service);
        }
    }
}
