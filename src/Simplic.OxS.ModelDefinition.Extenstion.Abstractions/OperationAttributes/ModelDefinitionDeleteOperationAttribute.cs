namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    /// <summary>
    /// Attribute to define a delete endpoint to appear in the model definition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ModelDefinitionDeleteOperationAttribute : Attribute
    {
        private readonly string endpoint;

        /// <summary>
        /// Initializes a delete endoint to appear in the model definition.
        /// </summary>
        /// <param name="endpoint">The endpoint of the delete.</param>
        public ModelDefinitionDeleteOperationAttribute(string endpoint)
        {
            this.endpoint = endpoint;
        }

        public string Endpoint => endpoint;
    }
}
