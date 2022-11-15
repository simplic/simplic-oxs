namespace Simplic.OxS.Server
{
    /// <summary>
    /// Represents a validation request send for each patched property.
    /// </summary>
    public class ValidationRequest
    {
        /// <summary>
        /// Gets or sets the property name.
        /// </summary>
        public string Property { get; set; }

        /// <summary>
        /// Gets or sets the full path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the value that will be set, when available.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the type of the validation request.
        /// </summary>
        public ValidationRequestType Type { get; set; }

        /// <summary>
        /// Gets or sets the patch item.
        /// </summary>
        public object PatchItem { get; set; }

        /// <summary>
        /// Gets or sets the original item.
        /// </summary>
        public object OriginalItem { get; set; }
    }
}
