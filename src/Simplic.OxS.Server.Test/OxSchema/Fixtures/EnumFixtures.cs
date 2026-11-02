namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>A plain enum, backed by a byte.</summary>
    public enum Mode : byte
    {
        First = 0,
        Second = 1,
    }

    /// <summary>A flags enum whose last member combines two others.</summary>
    [Flags]
    public enum Access : short
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write,
    }

    /// <summary>An enum with one retired member.</summary>
    public enum Retired
    {
        Live = 0,

        [Obsolete("kept for historical data")]
        Dead = 1,
    }

    /// <summary>
    /// An unsigned enum whose top member has the high bit set.
    /// </summary>
    /// <remarks>
    /// The wire carries enum values as signed 64-bit numbers, so this member is the one that
    /// proves the conversion is unchecked - a checked cast would abort the build.
    /// </remarks>
    public enum Wide : ulong
    {
        Low = 1,
        Top = 0x8000000000000000,
    }
}
