﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.Server.Settings;
using System.Text;

namespace Simplic.OxS.Server.Extensions
{
    internal static class JwtAuthenticationExtension
    {
        internal static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AuthSettings>(options => configuration.GetSection("Auth").Bind(options));
            var authSettings = configuration.GetSection("Auth").Get<AuthSettings>();

            if (authSettings != null)
            {
                services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;

                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Token)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
            }

            return services;
        }
    }
}
