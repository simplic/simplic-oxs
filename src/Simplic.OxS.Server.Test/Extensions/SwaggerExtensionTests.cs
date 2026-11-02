using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.Test.OxSchema;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Test.Extensions
{
    /// <summary>Which comment files of the query engine the OpenAPI setup includes.</summary>
    public sealed class SwaggerExtensionTests : IDisposable
    {
        private const string Comments = """
            <?xml version="1.0"?>
            <doc>
              <assembly><name>OxQL.Sample</name></assembly>
              <members>
                <member name="T:OxQL.Sample.Body"><summary>A body.</summary></member>
              </members>
            </doc>
            """;

        private readonly string directory = Directory.CreateTempSubdirectory("oxs-openapi-comments-").FullName;

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(directory, recursive: true);

        private string Write(string file, string content)
        {
            var path = Path.Combine(directory, file);

            File.WriteAllText(path, content);

            return path;
        }

        [Fact]
        public void EveryCommentFileOfTheQueryEngineBesideTheAssemblies_IsIncluded_AndNoOtherFile()
        {
            Write("OxQL.AspNetCore.xml", Comments);
            Write("OxQL.Core.xml", Comments);
            Write("Other.Service.xml", Comments);

            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();

            SwaggerExtension.IncludeQueryEngineComments(options, directory, log.CreateLogger("test"));

            options.SchemaFilterDescriptors.Should().HaveCount(2);
            log.Entries.Should().BeEmpty();
        }

        [Fact]
        public void ACommentFileThatCannotBeRead_IsLeftOutWithAWarning_AndTheOthersAreIncluded()
        {
            Write("OxQL.AspNetCore.xml", Comments);
            Write("OxQL.Broken.xml", "<doc><members><member name=");

            var locked = Write("OxQL.Locked.xml", Comments);
            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();

            using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var include = () => SwaggerExtension.IncludeQueryEngineComments(options, directory, log.CreateLogger("test"));

                include.Should().NotThrow("a comment file must not stand between a service and its OpenAPI document");
            }

            options.SchemaFilterDescriptors.Should().ContainSingle("the file that reads is included");
            log.Entries.Should().HaveCount(2).And.OnlyContain(entry => entry.Level == LogLevel.Warning && entry.Exception != null);
            log.Entries.Select(entry => entry.Message).Should().Contain(message => message.Contains("OxQL.Broken.xml", StringComparison.Ordinal))
                .And.Contain(message => message.Contains("OxQL.Locked.xml", StringComparison.Ordinal));
        }

        [Fact]
        public void ADirectoryThatIsNotThere_IncludesNothingAndDoesNotThrow()
        {
            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();
            var include = () => SwaggerExtension.IncludeQueryEngineComments(options, Path.Combine(directory, "missing"), log.CreateLogger("test"));

            include.Should().NotThrow();
            options.SchemaFilterDescriptors.Should().BeEmpty();
            log.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
        }
    }
}
