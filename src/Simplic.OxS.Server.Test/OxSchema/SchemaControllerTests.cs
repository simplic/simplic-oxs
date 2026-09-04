using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Controller;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The schema endpoint: its headers and its revalidation answer.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaControllerTests
    {
        private static SchemaController Endpoint(OxSchemaRegistry registry, string? ifNoneMatch = null)
        {
            var context = new DefaultHttpContext();

            if (ifNoneMatch is not null)
                context.Request.Headers.IfNoneMatch = ifNoneMatch;

            return new SchemaController(registry)
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };
        }

        [Fact]
        public void Get_WithoutARevalidationHeader_ReturnsTheDocument()
        {
            var registry = SchemaBuild.Degraded;
            var endpoint = Endpoint(registry);

            var file = endpoint.Get(CancellationToken.None).Should().BeOfType<FileContentResult>().Subject;

            file.ContentType.Should().Be("application/json");
            file.FileContents.Should().Equal(registry.Body);
        }

        [Fact]
        public void Get_AlwaysSetsThePrivateCacheHeaders()
        {
            var registry = SchemaBuild.Degraded;
            var endpoint = Endpoint(registry);

            endpoint.Get(CancellationToken.None);

            endpoint.Response.Headers.CacheControl.ToString().Should().Be("private, must-revalidate");
            endpoint.Response.Headers.ETag.ToString().Should().Be(registry.ETag);
        }

        [Fact]
        public void Get_WithTheCurrentTag_ReturnsNotModified()
        {
            var registry = SchemaBuild.Degraded;

            var answer = Endpoint(registry, registry.ETag).Get(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        }

        [Fact]
        public void Get_WithTheCurrentTagMarkedWeak_ReturnsNotModified()
        {
            var registry = SchemaBuild.Degraded;

            var answer = Endpoint(registry, $"W/{registry.ETag}").Get(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        }

        [Fact]
        public void Get_WithAWildcard_ReturnsNotModified()
        {
            var answer = Endpoint(SchemaBuild.Degraded, "*").Get(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        }

        [Fact]
        public void Get_WithAListContainingTheCurrentTag_ReturnsNotModified()
        {
            var registry = SchemaBuild.Degraded;

            var answer = Endpoint(registry, $"\"stale\", {registry.ETag}").Get(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        }

        [Fact]
        public void Get_WithAStaleTag_ReturnsTheDocument()
        {
            var registry = SchemaBuild.Degraded;

            var answer = Endpoint(registry, "\"0000000000000000000000000000000000000000000000000000000000000000\"").Get(CancellationToken.None);

            answer.Should().BeOfType<FileContentResult>()
                .Which.FileContents.Should().Equal(registry.Body);
        }

        [Fact]
        public void Get_WithAnEmptyRevalidationHeader_ReturnsTheDocument()
        {
            var answer = Endpoint(SchemaBuild.Degraded, "").Get(CancellationToken.None);

            answer.Should().BeOfType<FileContentResult>();
        }
    }
}
