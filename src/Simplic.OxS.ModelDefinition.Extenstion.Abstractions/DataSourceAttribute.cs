namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class DataSourceAttribute : Attribute
    {
        private readonly string endpoint;
        private readonly DataSourceType type;
        private readonly string? gqlEntryPoint;

        public DataSourceAttribute(DataSourceType type, string endpoint, string? gqlEntryPoint)
        {
            this.endpoint = endpoint;
            this.type = type;
            this.gqlEntryPoint = gqlEntryPoint;
        }

        public DataSourceType Type => type;

        public string Endpoint => endpoint;

        public string? GqlEntryPoint => gqlEntryPoint;
    }
}
