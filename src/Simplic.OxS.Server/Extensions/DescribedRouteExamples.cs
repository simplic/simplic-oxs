using OxQL.AspNetCore.OpenApi;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// The examples the OpenAPI document carries for the routes of this package: one request and
    /// one answer per route, short, of an invented parcel service. An example shows the form of a
    /// body; what a member means is said where the format and the query language are documented.
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

        /// <summary>The body of <c>POST /AddonDefinition</c>.</summary>
        public const string CreateAddonDefinition = """
            {
              "entity": "parcel.parcel",
              "path": "handling",
              "kind": "string",
              "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" } ],
              "displayName": "Handling",
              "description": "How the parcel is to be handled on its way."
            }
            """;

        /// <summary>An addon definition, as <c>POST /AddonDefinition</c> and <c>GET /AddonDefinition/by-id/{id}</c> answer it.</summary>
        public const string AddonDefinition = """
            {
              "id": "3c8e1f5a-7b24-4d09-a6c1-9e0f2b4d6a18",
              "entity": "parcel.parcel",
              "path": "handling",
              "kind": "string",
              "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" } ],
              "displayName": "Handling",
              "description": "How the parcel is to be handled on its way.",
              "retired": false
            }
            """;

        /// <summary>The answer of <c>GET /AddonDefinition/{entity}</c>: a definition and a retired one.</summary>
        public const string AddonDefinitions = """
            [
              {
                "id": "3c8e1f5a-7b24-4d09-a6c1-9e0f2b4d6a18",
                "entity": "parcel.parcel",
                "path": "handling",
                "kind": "string",
                "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" } ],
                "displayName": "Handling",
                "description": "How the parcel is to be handled on its way.",
                "retired": false
              },
              {
                "id": "a41d7c9e-2f60-4b83-8d15-6c3e0a9b7f42",
                "entity": "parcel.parcel",
                "path": "insuredValue",
                "kind": "decimal",
                "displayName": "Insured value",
                "retired": true
              }
            ]
            """;

        /// <summary>The body of <c>PUT /AddonDefinition/{id}</c>.</summary>
        public const string UpdateAddonDefinition = """
            {
              "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" }, { "value": "cool", "label": "Keep cool" } ],
              "displayName": "Handling",
              "description": "How the parcel is to be handled on its way."
            }
            """;

        /// <summary>The answer of <c>PUT /AddonDefinition/{id}</c>.</summary>
        public const string UpdatedAddonDefinition = """
            {
              "id": "3c8e1f5a-7b24-4d09-a6c1-9e0f2b4d6a18",
              "entity": "parcel.parcel",
              "path": "handling",
              "kind": "string",
              "values": [ { "value": "fragile", "label": "Fragile" }, { "value": "upright", "label": "Keep upright" }, { "value": "cool", "label": "Keep cool" } ],
              "displayName": "Handling",
              "description": "How the parcel is to be handled on its way.",
              "retired": false
            }
            """;

        /// <summary>
        /// The request and answer examples of a route of the query engine, told by the model of
        /// its body: the query, the batch, the explain, and health, which has none.
        /// </summary>
        public static (string? Request, string? Answer) OfQueryRoute(Type? body)
        {
            if (body is null)
                return (null, HealthAnswer);

            if (body == typeof(OxQLQueryBody))
                return (Query, QueryAnswer);

            if (body == typeof(OxQLBatchBody))
                return (Batch, BatchAnswer);

            return body == typeof(OxQLExplainBody) ? (Explain, ExplainAnswer) : (null, null);
        }

        /// <summary>The body of <c>POST /OxQL/query</c>.</summary>
        public const string Query = """
            {
              "entityType": "parcel.parcel",
              "pipeline": [
                { "match": { "weightKg": { "gte": 5 } } },
                { "sort": [ { "label": "asc" } ] },
                { "page": { "limit": 2, "count": true } }
              ]
            }
            """;

        /// <summary>The answer of <c>POST /OxQL/query</c>.</summary>
        public const string QueryAnswer = """
            {
              "items": [
                { "id": "6f1c2f0a-8d3b-4c55-9a70-2b1e5d7c9f10", "label": "P-1001", "weightKg": 7.5 },
                { "id": "0b9e4d21-5a67-4f3c-8e12-7c6d5b4a3f29", "label": "P-1002", "weightKg": 12 }
              ],
              "pageInfo": {
                "hasNextPage": true,
                "nextCursor": "eyJ2IjoyLCJrIjpbIlAtMTAwMiJdfQ.c2lnbmF0dXJl",
                "count": { "state": "exact", "value": 14 }
              }
            }
            """;

        /// <summary>The body of <c>POST /OxQL/batch</c>.</summary>
        public const string Batch = """
            {
              "queries": [
                { "entityType": "parcel.parcel", "pipeline": [ { "sort": [ { "label": "asc" } ] }, { "page": { "limit": 1 } } ] },
                { "entityType": "parcel.parcel", "pipeline": [ { "match": { "wieghtKg": { "gte": 5 } } } ] }
              ],
              "maxTimeMs": 2000
            }
            """;

        /// <summary>The answer of <c>POST /OxQL/batch</c>: a page, and the refusal of the second query in its place.</summary>
        public const string BatchAnswer = """
            {
              "results": [
                {
                  "items": [ { "id": "6f1c2f0a-8d3b-4c55-9a70-2b1e5d7c9f10", "label": "P-1001", "weightKg": 7.5 } ],
                  "pageInfo": { "hasNextPage": true, "nextCursor": "eyJ2IjoyLCJrIjpbIlAtMTAwMSJdfQ.c2lnbmF0dXJl", "count": { "state": "atLeast", "value": 2, "stoppedBy": "page" } }
                },
                {
                  "type": "validation_error",
                  "title": "The request could not be bound.",
                  "status": 400,
                  "errors": [ { "code": "UNKNOWN_PATH", "message": "'wieghtKg' is not a path of 'parcel.parcel'.", "stage": 0, "path": "wieghtKg" } ]
                }
              ]
            }
            """;

        /// <summary>The body of <c>POST /OxQL/explain</c>.</summary>
        public const string Explain = """
            {
              "query": {
                "entityType": "parcel.parcel",
                "pipeline": [ { "match": { "weightKg": { "gte": 5 } } }, { "page": { "limit": 2 } } ]
              },
              "include": ["notes"]
            }
            """;

        /// <summary>The answer of <c>POST /OxQL/explain</c>.</summary>
        public const string ExplainAnswer = """
            {
              "valid": true,
              "executable": "yes",
              "contract": 2,
              "engine": { "version": "2.1.0.0", "contract": 2 },
              "etag": "W/\"x3:4575645a135436d89b17f6ade5f1f30d\"",
              "errors": [],
              "diagnostics": [],
              "notes": [],
              "stages": [
                { "index": 0, "kind": "match", "status": "ok" },
                { "index": 1, "kind": "page", "status": "ok" }
              ]
            }
            """;

        /// <summary>The answer of <c>GET /OxQL/health</c> to a contract 2 caller.</summary>
        public const string HealthAnswer = """
            {
              "status": "healthy",
              "service": "oxql",
              "engine": { "version": "2.1.0.0", "contract": 2 },
              "capabilities": ["batch", "explain", "resolve.remote"],
              "limits": { "maxPageSize": 500, "defaultPageSize": 100, "maxPipelineStages": 20, "maxBatchQueries": 10 },
              "remote": [
                {
                  "service": "depot", "configured": true, "reachable": true, "contract": 2, "engineVersion": "2.1.0.0", "usable": true,
                  "references": { "declared": 3, "validated": true, "revision": "sha256:9c1b0e7d5a3f4c2b8e6d4f2a1c3b5d7e9f1a2b3c4d5e6f708192a3b4c5d6e7f8", "unknownTargets": [] }
                }
              ],
              "model": { "entities": 1, "degraded": false }
            }
            """;

        /// <summary>A refusal, as every 400 of the query routes is written.</summary>
        public const string Refusal = """
            {
              "type": "validation_error",
              "title": "The request could not be bound.",
              "status": 400,
              "errors": [ { "code": "UNKNOWN_PATH", "message": "'wieghtKg' is not a path of 'parcel.parcel'.", "stage": 0, "path": "wieghtKg" } ]
            }
            """;
    }
}
