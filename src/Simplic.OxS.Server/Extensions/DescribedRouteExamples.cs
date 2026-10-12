namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// The examples the OpenAPI document carries for the routes of this package: one per route,
    /// short, of an invented parcel service. An example shows the form of a body; what a member
    /// means is said where the format is documented.
    /// </summary>
    internal static class DescribedRouteExamples
    {
        /// <summary>The answer of <c>GET /schema</c>.</summary>
        public const string Schema = """
            {
              "schemaVersion": "1.1",
              "service": "parcel",
              "api": { "name": "parcel-api", "version": "v1" },
              "revision": "sha256:5f2c7a9e1b0d4c3a8e6f5d4c3b2a19087f6e5d4c3b2a1908f7e6d5c4b3a29180",
              "limits": {
                "maxPageSize": 500, "defaultPageSize": 100, "maxPipelineStages": 20, "maxLookupStages": 5,
                "maxUnwindStages": 5, "maxGroupFields": 40, "maxProjectionFields": 500, "regexMaxLength": 200,
                "maxOffset": 5000, "maxResolveStages": 8, "maxBatchQueries": 10, "maxLookupLimit": 100,
                "maxContinuedStages": 8, "maxFlattenDepth": 5, "maxReportPageSize": 5000
              },
              "types": {
                "parcel.parcel": {
                  "displayName": "Parcel",
                  "entity": true,
                  "aliases": [],
                  "key": ["id"],
                  "display": "label",
                  "extendable": true,
                  "queryable": true,
                  "notFilterable": [],
                  "notSortable": [],
                  "items": [],
                  "properties": [
                    { "name": "id", "kind": "guid" },
                    { "name": "label", "kind": "string" },
                    { "name": "weightKg", "kind": "decimal" }
                  ]
                }
              }
            }
            """;

        /// <summary>The answer of <c>GET /schema/addons</c>.</summary>
        public const string SchemaAddons = """
            {
              "parcel.parcel": [
                { "name": "handling", "kind": "string", "nullable": true, "displayName": "Handling",
                  "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" } ] }
              ]
            }
            """;
    }
}
