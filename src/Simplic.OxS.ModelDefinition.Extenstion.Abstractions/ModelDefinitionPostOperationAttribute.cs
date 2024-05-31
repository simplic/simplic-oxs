namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ModelDefinitionPostOperationAttribute : Attribute
    {
        private readonly string endpoint;
        private readonly Type response;
        private readonly Type request;

        public ModelDefinitionPostOperationAttribute(string endpoint, Type request, Type response)
        {
            this.endpoint = endpoint;
            this.response = response;
            this.request = request;
        }

        public string Endpoint { get => endpoint; }

        public Type Request { get => request; }

        public Type Response { get => response; }
    }
}
