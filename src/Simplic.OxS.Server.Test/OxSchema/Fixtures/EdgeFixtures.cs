using System.Collections;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>An enum whose member name is not ASCII, so the body has something to escape.</summary>
    public enum Umlaut
    {
        Größe = 0,
    }

    /// <summary>An entity with a collection that declares no element type and a member named outside ASCII.</summary>
    [OxQLType("probe.bag", "probe.bag")]
    public class Bag : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public ArrayList Untyped { get; set; } = [];

        public Umlaut Size { get; set; }
    }

    /// <summary>The base half of two declarations that resolve to one CLR type.</summary>
    [OxQLType("probe.base", "probe.base")]
    public class SharedBase : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>The derived half: the registry resolves a declaration on the base to its most derived subclass.</summary>
    [OxQLType("probe.leaf", "probe.leaf")]
    public class SharedLeaf : SharedBase
    {
        public string? Extra { get; set; }
    }
}
