using System.Reflection;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>An assembly of exactly the listed types: a build over a chosen few of the fixtures.</summary>
    internal sealed class TypeList(params Type[] types) : Assembly
    {
        public override Type[] GetTypes() => types;
    }
}
