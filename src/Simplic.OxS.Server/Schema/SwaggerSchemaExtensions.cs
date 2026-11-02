using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Server;

public static class SwaggerSchemaExtensions
{
    /// <summary>
    /// Configure swagger to use custom swagger model ids (<see cref="SwaggerSchemaIdAttribute"/>).
    /// </summary>
    public static void UseCustomSwaggerId(this IServiceCollection services)
    {
        services.ConfigureSwaggerGen(options =>
            {
                options.CustomSchemaIds(SwaggerSchemaIdAttribute.GetSchemaId);
            });
    }
}
