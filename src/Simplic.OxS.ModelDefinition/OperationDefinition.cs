namespace Simplic.OxS.ModelDefinition
{
    public class OperationDefinition
    {
        public string Type { get; set; }

        public string Endpoint { get; set; }

        public string? RequestReference { get; set; }

        public string? ResponseReference { get; set; }
    }
}
