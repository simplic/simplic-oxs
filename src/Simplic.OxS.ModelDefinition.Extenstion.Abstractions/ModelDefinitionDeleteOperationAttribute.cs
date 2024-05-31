namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class ModelDefinitionDeleteOperationAttribute : Attribute
    {
        private readonly string endpoint;

        public ModelDefinitionDeleteOperationAttribute(string endpoint)
        {
            this.endpoint = endpoint;
        }

        public string Endpoint { get => endpoint; }
    }
}
