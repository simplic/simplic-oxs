using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Extensions;

public static class GrpcExtension
{
    /// <summary>
    /// Add Grpc-Server components
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddGrpcServer(this IServiceCollection services)
    {
        services.AddGrpc();

        return services;
    }
}
