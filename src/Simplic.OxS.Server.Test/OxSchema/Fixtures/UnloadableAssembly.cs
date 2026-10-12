using System.Reflection;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>An assembly whose type list cannot be loaded: what a scan sees when a transitive dependency is missing from the output folder.</summary>
    internal sealed class UnloadableAssembly : Assembly
    {
        /// <summary>The missing dependency the loader detail names.</summary>
        internal const string MissingDependency = "Fictional.Driver";

        public override Type[] GetTypes() => throw new ReflectionTypeLoadException(
            [],
            [
                new FileNotFoundException(
                    "Could not load file or assembly '" + MissingDependency
                    + ", Version=9.9.9.9, Culture=neutral, PublicKeyToken=null'. "
                    + "The system cannot find the file specified."),
            ]);
    }
}
