using System.Diagnostics;
using System.IO.Compression;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What the package holds for a service's build: the targets a referencing project imports
    /// and, beside them, the two comment files those targets copy: the query engine's HTTP
    /// assembly's and the package's own. The test packs the server project as the pipeline does,
    /// without building, and reads the archive. A restore that leaves documentation files out
    /// drops the <c>.xml</c> beside an assembly under <c>lib</c> and <c>ref</c> and nothing under
    /// <c>build</c>, so the place of the files is what a service's OpenAPI document depends on.
    /// </summary>
    public sealed class PackageLayoutTests
    {
        private const string Comments = "OxQL.AspNetCore.xml";
        private const string OwnComments = "Simplic.OxS.Server.xml";
        private const string Targets = "Simplic.OxS.Server.targets";

        [Fact]
        public void ThePackage_HoldsTheQueryEnginesCommentsAndItsOwnBesideItsTargets_WhereNoRestoreLeavesThemOut()
        {
            var output = Directory.CreateTempSubdirectory("simplic-oxs-pack-").FullName;

            try
            {
                Pack(output);

                var package = Directory.GetFiles(output, "Simplic.OxS.Server.*.nupkg").Should().ContainSingle().Subject;
                using var archive = ZipFile.OpenRead(package);
                var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

                entries.Where(entry => entry.StartsWith("build", StringComparison.Ordinal)).Should().BeEquivalentTo(
                    [
                        $"build/{Targets}", $"build/{Comments}", $"build/{OwnComments}",
                        $"buildTransitive/{Targets}", $"buildTransitive/{Comments}", $"buildTransitive/{OwnComments}",
                    ],
                    "a project imports the targets from build or buildTransitive, and they copy the files beside them");

                entries.Where(entry => entry.EndsWith(Comments, StringComparison.OrdinalIgnoreCase)).Should().OnlyContain(
                    entry => entry.StartsWith("build", StringComparison.Ordinal),
                    "under lib and ref a restore without documentation files leaves the file out");

                // The package's own comments are also where every package has them, beside the assembly: that copy is the one a restore may leave out.
                entries.Where(entry => entry.EndsWith(OwnComments, StringComparison.OrdinalIgnoreCase) && !entry.StartsWith("build", StringComparison.Ordinal))
                    .Should().Equal($"lib/net10.0/{OwnComments}");

                // The comments are those of the engine assembly the package is built against, and those of the package's own build.
                foreach (var file in new[] { Comments, OwnComments })
                {
                    var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, file));

                    foreach (var folder in new[] { "build", "buildTransitive" })
                    {
                        Read(archive, $"{folder}/{file}").Should().Equal(expected, $"{folder} holds {file} of the build the package is packed from");

                        System.Text.Encoding.UTF8.GetString(Read(archive, $"{folder}/{Targets}")).Should().Contain(
                            $"$(MSBuildThisFileDirectory){file}",
                            "the targets take the file from their own folder");
                    }
                }
            }
            finally
            {
                Directory.Delete(output, recursive: true);
            }
        }

        /// <summary>
        /// The targets of the package, imported by this project, at publish: each comment file
        /// is in the output once, also where the project lists a file of that name itself (this
        /// project's reference to the server project lists the package's own, as a package
        /// restored with documentation files would). Two files of one name stop a publish, so
        /// a publish that passes holds the replacement. What stays to a manual run is the same
        /// from a restored package (a consumer project against a local feed).
        /// </summary>
        [Fact]
        public void APublishOfAProjectThatImportsTheTargets_HoldsEachCommentFileOnce()
        {
            var output = Directory.CreateTempSubdirectory("simplic-oxs-publish-").FullName;

            try
            {
                var project = Path.Combine(Path.GetDirectoryName(ServerProject())!, "..", "Simplic.OxS.Server.Hosting.Test", "Simplic.OxS.Server.Hosting.Test.csproj");

                Run("publish", Path.GetFullPath(project), "--no-build", "--nologo", "-c", Configuration(), "-o", output);

                foreach (var file in new[] { Comments, OwnComments })
                {
                    Directory.GetFiles(output, file, SearchOption.AllDirectories).Should().ContainSingle($"{file} is published once")
                        .Which.Should().Be(Path.Combine(output, file));
                    File.ReadAllBytes(Path.Combine(output, file)).Should().Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, file)), $"{file} is the one of the build");
                }
            }
            finally
            {
                Directory.Delete(output, recursive: true);
            }
        }

        private static byte[] Read(ZipArchive archive, string name)
        {
            using var stream = archive.GetEntry(name)!.Open();
            using var bytes = new MemoryStream();

            stream.CopyTo(bytes);

            return bytes.ToArray();
        }

        /// <summary>Packs the server project in the configuration of this test run, from what is built.</summary>
        private static void Pack(string output) =>
            Run("pack", ServerProject(), "--no-build", "--nologo", "-c", Configuration(), "-o", output);

        /// <summary>Runs <c>dotnet</c> with the arguments and expects it to succeed.</summary>
        private static void Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);

            // A test host started by the SDK hands its own MSBuild locations down; the pack resolves its own.
            foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(name);

            using var process = Process.Start(start)!;

            var errors = process.StandardError.ReadToEndAsync();
            var text = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            process.ExitCode.Should().Be(0, $"dotnet {arguments[0]} --no-build succeeds:\n{text}\n{errors.Result}");
        }

        private static string ServerProject()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var project = Path.Combine(directory.FullName, "Simplic.OxS.Server", "Simplic.OxS.Server.csproj");

                if (File.Exists(project))
                    return project;
            }

            throw new InvalidOperationException($"No Simplic.OxS.Server project above {AppContext.BaseDirectory}.");
        }

        /// <summary>The configuration this assembly is built in: <c>bin/{configuration}/{framework}</c>.</summary>
        private static string Configuration() =>
            new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Parent!.Name;
    }
}
