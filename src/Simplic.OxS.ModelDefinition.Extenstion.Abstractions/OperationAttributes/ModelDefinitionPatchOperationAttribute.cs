namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ModelDefinitionPatchOperationAttribute : Attribute
    {
        private readonly string endpoint;
        private readonly Type response;
        private readonly Type request;

        public ModelDefinitionPatchOperationAttribute(string endpoint, Type request, Type response)
        {
            this.endpoint = endpoint;
            this.response = response;
            this.request = request;
        }

        public string Endpoint => endpoint;

        public Type Request => request;

        public Type Response => response;
    }   
}
