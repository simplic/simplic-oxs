using System.Diagnostics;
using System.IO.Compression;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What the package holds for a service's build: the targets a referencing project imports
    /// and, beside them, the comments of the query engine's HTTP assembly those targets copy. The
    /// test packs the server project as the pipeline does, without building, and reads the
    /// archive. A restore that leaves documentation files out drops the <c>.xml</c> beside an
    /// assembly under <c>lib</c> and <c>ref</c> and nothing under <c>build</c>, so the place of
    /// the file is what a service's OpenAPI document depends on.
    /// </summary>
    public sealed class PackageLayoutTests
    {
        private const string Comments = "OxQL.AspNetCore.xml";
        private const string Targets = "Simplic.OxS.Server.targets";

        [Fact]
        public void ThePackage_HoldsTheQueryEnginesCommentsBesideItsTargets_AndNowhereARestoreLeavesThemOut()
        {
            var output = Directory.CreateTempSubdirectory("simplic-oxs-pack-").FullName;

            try
            {
                Pack(output);

                var package = Directory.GetFiles(output, "Simplic.OxS.Server.*.nupkg").Should().ContainSingle().Subject;
                using var archive = ZipFile.OpenRead(package);
                var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

                entries.Where(entry => entry.StartsWith("build", StringComparison.Ordinal)).Should().BeEquivalentTo(
                    [$"build/{Targets}", $"build/{Comments}", $"buildTransitive/{Targets}", $"buildTransitive/{Comments}"],
                    "a project imports the targets from build or buildTransitive, and they copy the file beside them");

                entries.Where(entry => entry.EndsWith(Comments, StringComparison.OrdinalIgnoreCase)).Should().OnlyContain(
                    entry => entry.StartsWith("build", StringComparison.Ordinal),
                    "under lib and ref a restore without documentation files leaves the file out");

                // The comments are those of the engine assembly the package is built against.
                var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, Comments));

                foreach (var folder in new[] { "build", "buildTransitive" })
                {
                    Read(archive, $"{folder}/{Comments}").Should().Equal(expected, $"{folder} holds the comments of the referenced engine version");

                    System.Text.Encoding.UTF8.GetString(Read(archive, $"{folder}/{Targets}")).Should().Contain(
                        $"$(MSBuildThisFileDirectory){Comments}",
                        "the targets take the file from their own folder");
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
        private static void Pack(string output)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in new[] { "pack", ServerProject(), "--no-build", "--nologo", "-c", Configuration(), "-o", output })
                start.ArgumentList.Add(argument);

            // A test host started by the SDK hands its own MSBuild locations down; the pack resolves its own.
            foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(name);

            using var process = Process.Start(start)!;

            var errors = process.StandardError.ReadToEndAsync();
            var text = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            process.ExitCode.Should().Be(0, $"dotnet pack --no-build packs the server project:\n{text}\n{errors.Result}");
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
