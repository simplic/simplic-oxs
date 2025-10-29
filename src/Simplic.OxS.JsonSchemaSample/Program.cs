using Simplic.OxS.Server.Service;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

// Generate schema
var mi = typeof(Sample).GetMethod(nameof(Sample.CreateUser))!;
var schema = SchemaGenerator.GenerateMethodJsonSchema(mi);

var opts = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

string schemaJson = JsonSerializer.Serialize(schema, opts);


// Or write to file
Console.WriteLine(schemaJson);