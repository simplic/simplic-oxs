namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SourceAttribute : Attribute
    {
        private readonly string endpoint;
        private readonly string sourceUrl;

        public SourceAttribute(string sourceUrl, string endpoint)
        {
            this.endpoint = endpoint;
            this.sourceUrl = sourceUrl;
        }

        public string SourceUrl => sourceUrl;

        public string Endpoint => endpoint;
    }
}
