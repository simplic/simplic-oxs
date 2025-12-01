using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server.Middleware;

namespace Simplic.OxS.Server.Extensions;

internal static class GrpcExtension
{
    /// <summary>
    /// Add Grpc-Server components
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddGrpcServer(this IServiceCollection services, Action<Grpc.AspNetCore.Server.GrpcServiceOptions> configureGrpc)
    {
        Console.WriteLine("Add gRPC / Protobug");
        services.AddGrpc(configureGrpc);

        Console.WriteLine(" > Register security interceptor");
        // Register gRPC security interceptor
        services.AddSingleton<GrpcSecurityInterceptor>();

        return services;
    }
}
