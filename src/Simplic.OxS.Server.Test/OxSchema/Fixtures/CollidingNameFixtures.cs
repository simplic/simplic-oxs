namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>Container for one of two nested types that share a name.</summary>
    public static class Alpha
    {
        /// <summary>Separated from its twin by the nesting chain alone.</summary>
        public class Detail
        {
            public string? Text { get; set; }
        }
    }

    /// <summary>Container for the other of two nested types that share a name.</summary>
    public static class Beta
    {
        /// <summary>Separated from its twin by the nesting chain alone.</summary>
        public class Detail
        {
            public int Number { get; set; }
        }
    }

    /// <summary>A generic whose closures pool separately and are separated by their type arguments alone.</summary>
    public class Pair<T>
    {
        public T? First { get; set; }

        public T? Second { get; set; }
    }
}
