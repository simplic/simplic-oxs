namespace Simplic.OxS.ServiceDefinition;

// Core JSON Schema node
public class JsonSchema
{
    // Common
    public string? Schema { get; set; } // maps to "$schema"
    public string? Title { get; set; }
    public string[]? Type { get; set; }        // allow multiple (e.g., ["string","null"])
    public string? SingleType { get; set; }    // convenience for single type during building
    public string? Description { get; set; }
    public string? Format { get; set; }
    public string? Ref { get; set; }           // maps to "$ref"

    // Enum / const
    public string[]? Enum { get; set; }
    public object? Const { get; set; }

    // Object-specific
    public Dictionary<string, JsonSchema>? Properties { get; set; }
    public string[]? Required { get; set; }
    public JsonSchema? AdditionalProperties { get; set; }

    // Array-specific
    public JsonSchema? Items { get; set; }
    public List<JsonSchema>? PrefixItems { get; set; }
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }

    // Compose final "type" per spec on serialization
    public void FinalizeTypes()
    {
        if (Type is null && SingleType is not null)
            Type = new[] { SingleType };
        SingleType = null;
    }
}

// Root document wrapper if you want a single place to include root metadata
public class MethodSchemaDocument
{
    public JsonSchema Root { get; set; } = new();
    public Dictionary<string, JsonSchema>? Definitions { get; set; }

    // Convenience to get a serializer-friendly object (optional)
    public object ToSerializable()
    {
        // Ensure all nodes have finalized Type[]
        void Walk(JsonSchema s)
        {
            s.FinalizeTypes();
            if (s.Properties != null)
                foreach (var p in s.Properties.Values) Walk(p);
            if (s.Items != null) Walk(s.Items);
            if (s.AdditionalProperties != null) Walk(s.AdditionalProperties);
            if (s.PrefixItems != null)
                foreach (var item in s.PrefixItems) Walk(item);
        }
        Walk(Root);
        if (Definitions != null)
            foreach (var d in Definitions.Values) Walk(d);

        // Render as anonymous object that maps to the schema keywords
        return new
        {
            // Root
            @schema = Root.Schema,      // "$schema"
            title = Root.Title,
            type = Root.Type,
            description = Root.Description,
            format = Root.Format,
            properties = Root.Properties,
            required = Root.Required,
            additionalProperties = Root.AdditionalProperties,
            items = Root.Items,
            prefixItems = Root.PrefixItems,
            minItems = Root.MinItems,
            maxItems = Root.MaxItems,
            // extra info for your method wrapper
            // (we keep these inside properties below instead)
            // definitions is a draft-07 concept; for 2020-12 you usually use $defs.
            // We'll expose both names for compatibility if you like; keeping $defs here:
            // Using "$defs" name:
            // defs = Definitions

            // If you prefer 2020-12’s "$defs", many serializers don’t like '$' in property names of anonymous objects.
            // You can serialize `Definitions` alongside manually with your serializer of choice.
        };
    }
}
