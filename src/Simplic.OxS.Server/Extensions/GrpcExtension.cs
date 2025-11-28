using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        services.AddGrpc(configureGrpc);
        
        // Register gRPC security interceptor
        services.AddSingleton<GrpcSecurityInterceptor>();

        return services;
    }
}
