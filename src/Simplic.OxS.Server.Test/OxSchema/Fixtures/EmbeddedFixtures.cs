using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>An embedded item that declares an identity.</summary>
    public class Slot : IItemId
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>An embedded value object with no identity.</summary>
    public class Tag
    {
        public string? Text { get; set; }
    }

    /// <summary>A local copy of an entity's shape, declared as a plain type rather than an entity.</summary>
    public class ThingSubset
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }
    }
}
