namespace Simplic.OxS.Server
{
    /// <summary>
    /// The type of the validation request.
    /// </summary>
    public enum ValidationRequestType : ushort
    {
        /// <summary>
        /// Indicates that a property will be updated.
        /// </summary>
        UpdateProperty = 0,

        /// <summary>
        /// Indicates that an item will be added.
        /// </summary>
        AddItem = 1,

        /// <summary>
        /// Indicates that an item will be removed.
        /// </summary>
        RemoveItem = 2
    }
}
