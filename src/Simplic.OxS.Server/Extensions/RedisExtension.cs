using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Cache;
using Simplic.OxS.Cache.Redis;
using Simplic.OxS.Cache.Service;
using Simplic.OxS.Server.Settings;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Redis service extension
    /// </summary>
    internal static class RedisExtension
    {
        /// <summary>
        /// Adds the redis extension
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Service configuration</param>
        /// <param name="redisEnabled">Gets whether redis was initizaled</param>
        /// <returns>Service collection instance</returns>
        internal static IServiceCollection AddRedisCaching(this IServiceCollection services, IConfiguration configuration, out string connection)
        {
            services.Configure<RedisSettings>(options => configuration.GetSection("Redis").Bind(options));
            var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>();

            if (redisSettings != null)
            {
                Console.WriteLine(" > Add redis caching");
                services.AddStackExchangeRedisCache(options => 
                {
                    options.Configuration = redisSettings.RedisCacheUrl;
                });

                services.AddTransient<ICacheRepository, CacheRepository>();
                services.AddTransient<ICacheService, CacheService>();

                connection = redisSettings.RedisCacheUrl;
            }
            else
            {
                Console.WriteLine(" > No redis context found.");
                connection = "";
            }

            return services;
        }
    }
}
