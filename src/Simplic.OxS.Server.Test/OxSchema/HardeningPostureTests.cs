using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Which environment variables put a host into the fail-fast posture.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningPostureTests
    {
        private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
            name => variables.Where(variable => variable.Name == name).Select(variable => variable.Value).FirstOrDefault();

        [Theory]
        [InlineData("CI", "true")]
        [InlineData("CI", "1")]
        [InlineData("TF_BUILD", "True")]
        public void ReadContinuousIntegration_AVariableOfABuildAgent_IsContinuousIntegration(string name, string value)
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(Environment((name, value))).Should().BeTrue();
        }

        [Fact]
        public void ReadContinuousIntegration_NoVariable_IsNotContinuousIntegration()
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(Environment()).Should().BeFalse();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("False")]
        [InlineData(" ")]
        public void ReadContinuousIntegration_EveryVariableTurnedOff_IsNotContinuousIntegration(string value)
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(Environment(("CI", value), ("TF_BUILD", value))).Should().BeFalse();
        }

        [Fact]
        public void ReadContinuousIntegration_OneVariableOffAndOneOn_IsContinuousIntegration()
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(Environment(("CI", "false"), ("TF_BUILD", "True"))).Should().BeTrue();
        }

        [Fact]
        public void ReadContinuousIntegration_AnUnrelatedVariable_IsIgnored()
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(Environment(("BUILD_BUILDID", "42"))).Should().BeFalse();
        }
    }
}
