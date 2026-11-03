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

            return new SchemaController(registry, global::OxQL.Model.Addon.EmptyAddonDefinitionSource.Instance, new Simplic.OxS.Server.Services.RequestContext())
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };
        }

        [Fact]
        public void Get_WithoutARevalidationHeader_ReturnsTheDocument()
        {
            var registry = SchemaBuild.Degraded;
            var endpoint = Endpoint(registry);

            var file = endpoint.Get(CancellationToken.None).Should().BeAssignableTo<FileContentResult>().Subject;

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

            answer.Should().BeAssignableTo<FileContentResult>()
                .Which.FileContents.Should().Equal(registry.Body);
        }

        private static async Task<(HttpResponse Response, byte[] Body)> WriteAsync(IActionResult result, string? acceptEncoding)
        {
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            if (acceptEncoding is not null)
                context.Request.Headers.AcceptEncoding = acceptEncoding;

            await result.ExecuteResultAsync(new ActionContext { HttpContext = context });

            return (context.Response, ((MemoryStream)context.Response.Body).ToArray());
        }

        private static byte[] Decoded(byte[] body, string encoding)
        {
            using var packed = new MemoryStream(body);
            using Stream coder = encoding == "br"
                ? new System.IO.Compression.BrotliStream(packed, System.IO.Compression.CompressionMode.Decompress)
                : new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionMode.Decompress);
            using var read = new MemoryStream();

            coder.CopyTo(read);

            return read.ToArray();
        }

        [Theory]
        [InlineData("br", "br")]
        [InlineData("gzip", "gzip")]
        [InlineData("gzip, deflate, br, zstd", "br")]
        [InlineData("br;q=0, gzip", "gzip")]
        public async Task Get_WritesTheDocumentInTheCodingTheCallerAccepts_CodedOnce(string acceptEncoding, string expected)
        {
            var registry = SchemaBuild.Degraded;

            registry.Body.Length.Should().BeGreaterThan(1_024);

            var (response, body) = await WriteAsync(Endpoint(registry).Get(CancellationToken.None), acceptEncoding);

            response.StatusCode.Should().Be(StatusCodes.Status200OK);
            response.ContentType.Should().Be("application/json; charset=utf-8");
            response.Headers.ContentEncoding.ToString().Should().Be(expected);
            response.Headers.Vary.ToString().Should().Be("Accept-Encoding");
            response.ContentLength.Should().Be(body.Length);
            body.Length.Should().BeLessThan(registry.Body.Length / 2);
            Decoded(body, expected).Should().Equal(registry.Body, "the coded body is the document, byte for byte");
            registry.Coded(expected).Should().BeSameAs(registry.Coded(expected), "the document is coded once per coding");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("identity")]
        [InlineData("deflate")]
        public async Task Get_WithoutACodingTheHostWrites_WritesTheDocumentAsItIs(string? acceptEncoding)
        {
            var registry = SchemaBuild.Degraded;

            var (response, body) = await WriteAsync(Endpoint(registry).Get(CancellationToken.None), acceptEncoding);

            response.Headers.ContentEncoding.Count.Should().Be(0);
            response.Headers.Vary.ToString().Should().Be("Accept-Encoding", "the body depends on that header, coded or not");
            body.Should().Equal(registry.Body);
        }

        [Fact]
        public void Get_WithAnEmptyRevalidationHeader_ReturnsTheDocument()
        {
            var answer = Endpoint(SchemaBuild.Degraded, "").Get(CancellationToken.None);

            answer.Should().BeAssignableTo<FileContentResult>();
        }
    }
}
