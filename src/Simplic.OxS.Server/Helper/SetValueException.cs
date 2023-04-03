namespace Simplic.OxS.Server
{
    /// <summary>
    /// Exception thrown when a value is failed to be set at the source object.
    /// </summary>
    public class SetValueException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetValueException"/>.
        /// </summary>
        /// <param name="propertyPath">The full path of the property that failed to be set.</param>
        public SetValueException(string propertyPath)
            :base($"Could not set value for property: '{propertyPath}'.")
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetValueException"/>.
        /// </summary>
        /// <param name="propertyPath">The full path of the property that failed to be set.</param>
        /// <param name="innerException">The inner exception</param>
        public SetValueException(string propertyPath, Exception? innerException)
            : base($"Could not set value for property: '{propertyPath}'.", innerException)
        {

        }
    }
}
