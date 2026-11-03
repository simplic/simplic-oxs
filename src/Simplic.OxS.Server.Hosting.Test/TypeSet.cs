using System.Reflection;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// An assembly of exactly the listed types: what a startup of these tests names as its
    /// entity assembly, so the schema of one startup never reaches the types of another. A class
    /// map is registered once per process, and a schema build freezes the map of every type it
    /// reaches.
    /// </summary>
    internal sealed class TypeSet(params Type[] types) : Assembly
    {
        public override Type[] GetTypes() => types;
    }
}
