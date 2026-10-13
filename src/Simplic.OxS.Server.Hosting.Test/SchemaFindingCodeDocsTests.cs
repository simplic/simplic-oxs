using System.Reflection;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// The description of the schema (<c>OX_SCHEMA.md</c>, at the root of the repository) names
    /// every finding code a start can log: the package's own and the query engine's model codes.
    /// </summary>
    public sealed class SchemaFindingCodeDocsTests
    {
        private static string Section(string from, string to)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var file = Path.Combine(directory.FullName, "OX_SCHEMA.md");

                if (!File.Exists(file))
                    continue;

                var text = File.ReadAllText(file);
                var start = text.IndexOf(from, StringComparison.Ordinal);
                var end = text.IndexOf(to, start + 1, StringComparison.Ordinal);

                start.Should().BePositive($"the description has the section '{from}'");

                return text[start..end];
            }

            throw new InvalidOperationException($"No OX_SCHEMA.md above {AppContext.BaseDirectory}: the test runs in the source tree.");
        }

        private static IEnumerable<string> CodesOf(Type constants) =>
            constants.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!);

        [Fact]
        public void TheSectionOnFindings_NamesEveryCodeOfThePackageAndOfTheQueryEnginesModel()
        {
            var section = Section("### 3.3 How the build fails", "### 3.4 ");
            var codes = CodesOf(typeof(OxSchemaCodes)).Concat(CodesOf(typeof(global::OxQL.Model.BuildCodes))).Distinct().ToList();

            codes.Should().HaveCountGreaterThan(20).And.Contain(["extendable-without-addon", "reference-target-unprefixed"]);
            codes.Where(code => !section.Contains($"`{code}`", StringComparison.Ordinal)).Should().BeEmpty("section 3.3 names every finding code in its tables");
        }

        [Fact]
        public void TheTableOfCosts_SaysOfEveryCodeOfThePackageWhatTheCodeSays()
        {
            var section = Section("### 3.3 How the build fails", "### 3.4 ");
            var rows = section.Split('\n').Where(line => line.StartsWith("| `", StringComparison.Ordinal) && line.TrimEnd().EndsWith('|')).ToList();

            foreach (var code in CodesOf(typeof(OxSchemaCodes)))
            {
                var row = rows.FirstOrDefault(line => line.Split('|')[1].Contains($"`{code}`", StringComparison.Ordinal) && line.Split('|').Length == 5);

                row.Should().NotBeNull($"the table of costs has a row for {code}");

                var cells = row!.Split('|');

                cells[2].Trim().Should().Be(OxSchemaCodes.Refuses(code) ? "yes" : "no", $"refuses, {code}");
                cells[3].Trim().Should().Be(OxSchemaCodes.IsPublished(code) ? "yes" : "no", $"published, {code}");
            }
        }
    }
}
