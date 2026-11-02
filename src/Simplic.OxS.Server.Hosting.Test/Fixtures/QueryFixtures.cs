using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OxQL.Core.Binding;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using OxQL.Model.Addon;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>
    /// A query engine that answers every query with one row naming the entity asked for, and
    /// keeps what it was asked: the hosts of these tests have no database, and what they test
    /// is what reaches the engine and what the routes answer around it.
    /// </summary>
    internal sealed class AnsweringEngine : IQueryEngine
    {
        /// <summary>Every query that reached the engine.</summary>
        public ConcurrentQueue<QueryRequest> Executed { get; } = new();

        /// <inheritdoc/>
        public Task<QueryOutcome> ExecuteAsync(QueryRequest request, RequestContext context, CancellationToken cancellationToken = default)
        {
            Executed.Enqueue(request);

            return Task.FromResult(QueryOutcome.Of(new QueryResult
            {
                Items = [new JsonObject { ["entity"] = request.EntityType }],
                PageInfo = new PageInfo { HasNextPage = false },
            }));
        }

        /// <inheritdoc/>
        public Task<ExplainOutcome> ExplainAsync(ExplainRequest request, RequestContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("These hosts explain nothing.");

        /// <summary>
        /// Registers the engine, and what stands in for the stores the host has none of: an
        /// organisation without addon definitions, and a cache in memory for the authentication.
        /// </summary>
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<IQueryEngine>(this);
            services.AddScoped<IAddonDefinitionSource>(_ => EmptyAddonDefinitionSource.Instance);
            services.AddDistributedMemoryCache();
        }
    }

    /// <summary>The credentials a caller of these hosts presents.</summary>
    internal static class Callers
    {
        /// <summary>The organisation every caller of these tests belongs to.</summary>
        public static readonly Guid Organisation = Guid.Parse("7d1f0c4e-3b52-4c8e-9a11-5e2f6d7c8b90");

        /// <summary>The user every caller of these tests is.</summary>
        public static readonly Guid User = Guid.Parse("2a9c4f10-6d3e-4b7a-8c21-0f1e2d3c4b5a");

        /// <summary>A bearer token of a user of <see cref="Organisation"/>, signed with the host's secret.</summary>
        public static string Bearer()
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(BootstrapHost.SigningSecret));
            var token = new JwtSecurityToken(
                claims: [new Claim("Id", User.ToString()), new Claim("OId", Organisation.ToString())],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
