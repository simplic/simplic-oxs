namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ModelDefinitionGetOperationAttribute : Attribute
    {
        private readonly string endpoint;
        private readonly Type response;

        public ModelDefinitionGetOperationAttribute(string endpoint, Type response)
        {
            this.endpoint = endpoint;
            this.response = response;
        }

        public string Endpoint => endpoint;

        public Type Response => response;
    }
}
