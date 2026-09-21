# The Ox Schema

Every service built on `Simplic.OxS.Server` publishes a schema document under `GET /schema`,
below its API base path (`/vehicle-api/v2/schema`). The document describes the entities the
service's query engine accepts, every type reachable from them, their keys, relationships and
REST operations, and the limits a query has to respect. It is built once at startup from the
service's own code, held in memory, and served with a content-derived revision and entity tag.

This file is the contract of that document (**format version 1.0**), followed by what a service
declares, how the document is built and how the build fails, how a host is configured
(section 4), and what a service gets and has to do when it upgrades to this package version
(section 5). The code is in `src/Simplic.OxS.Server/OxSchema/` and `src/Simplic.OxS.Server/OxQL/`;
the tests in `src/Simplic.OxS.Server.Test/OxSchema/` and `src/Simplic.OxS.Server.Test/OxQL/`.

---

## 1 · The document

### 1.1 Envelope

```jsonc
{
  "schemaVersion": "1.0",
  "service": "vehicle",
  "api": { "name": "vehicle-api", "version": "v2" },
  "revision": "sha256:…",
  "limits": {
    "maxPageSize": 500, "defaultPageSize": 100,
    "maxPipelineStages": 20, "maxLookupStages": 5, "maxUnwindStages": 5,
    "maxGroupFields": 20, "maxProjectionFields": 500, "regexMaxLength": 200,
    "maxOffset": 5000, "maxResolveStages": 2, "maxBatchQueries": 10, "maxLookupLimit": 100
  },
  "diagnostics": [ … ],                 // absent unless the build was degraded
  "types": { "<type id>": { … } }       // one flat pool: entities and structural types
}
```

| member | meaning |
|---|---|
| `schemaVersion` | `<major>.<minor>`. See *Compatibility*. |
| `service` | The service name, lower-case. |
| `api` | The two segments of the service's API base path, verbatim, the `v` included. The environment is not part of it; the origin is the caller's own. |
| `revision` | `sha256:` plus the digest of this document's canonical form with the `revision` member absent. Stable across restarts; identical for identical content. |
| `limits` | What a client can check a request against before sending it. Every value is the one the query engine enforces. The engine holds more limits than these - resolve chunking, the semi-join cap, the count cap - which a caller cannot act on in advance; `GET /oxql/health` publishes the whole set for diagnosis. |
| `diagnostics` | What the build could not describe. Absent on a clean build. |
| `types` | The type pool, keyed by type id, sorted ordinally. |

### 1.2 Canonical form

A client that verifies `revision`, or diffs two documents, needs the byte rule:

1. camelCase member names.
2. No insignificant whitespace.
3. A null member is written as an absent member. What absence *means* is per member (section 1.9).
4. Every map (`types`, `operations`) has its keys sorted by ordinal comparison.
5. Envelope member order is fixed as listed above. Within a pool entry or a property
   descriptor, member order is fixed too, as the tables below list them.
6. Array order is significant: `properties`, enum `values`, `items` and `aliases` are in the
   generator's order.
7. Every character outside the JSON encoder's unreserved set is written as a `\uXXXX` escape,
   so the document is pure ASCII.

**`revision` is verified textually.** Cut the substring `"revision":"sha256:<hex>"` and its one
separating comma out of the received bytes and hash the rest; do not re-serialise a parsed
document. The member occurs exactly once, as a direct child of the envelope, and no string value
can spell it.

### 1.3 Transport

```
200  ETag: "<the bare hex of the revision>"
     Cache-Control: private, must-revalidate
304  on a matching If-None-Match, no body
```

`If-None-Match` is honoured for `*`, for a comma-separated list and for weak (`W/"…"`) entries.
The document is only ever served whole, so weak and strong comparison coincide.

### 1.4 Two id spaces

| | entity id | structural type id |
|---|---|---|
| pool key | `vehicle.vehicle` | `t_carrierContact` |
| as a pointer | `#/types/vehicle.vehicle` | `#/types/t_carrierContact` |
| minted | declared on the entity, `<service>.<entity>`, `[a-z][a-z0-9_]*` per segment | from the CLR type name, document-local |
| stability | a contract: aliased forever, persisted in configurations | none |
| addressed by | id | a path from an entity id only |

**Entity ids are the document's only stable entry points.** Everything else is addressed by
path from an entity id, written `entityId#path` (`vehicle.vehicle#carrier.address.city`).

A structural id is `t_` plus the CLR type name in camelCase, with the generic arity cut
(`GeoJsonPoint<T>` is `t_geoJsonPoint`). When two pooled types share a name, *every* claimant
gets a tail: `_` plus the leading hex digits of a digest over the type's namespace, nesting
chain, generic arguments and assembly simple name, widened until unique. A structural id
changes when the type is renamed, when a second type of the same name enters the pool, and when
the type stops being reachable. A consumer may resolve one and must never persist one.

Pool keys are bare; only a pointer carries the `#/types/` prefix, for structural and entity
targets alike. Resolution: strip the prefix, look the remainder up in `types`.

### 1.5 Pool entry

An entity is a structural type that additionally carries entity metadata. Members, in order:

| member | on | meaning |
|---|---|---|
| `kind` | enum entries | `enum`. Absent on an object entry. |
| `displayName` | entities | The human label. |
| `description` | any | A description. Reserved: nothing populates it in this version. |
| `flags` | enum entries | Whether the enum is a flags enum. |
| `values` | enum entries | The members, in declaration order: `{ name, value, active }`. |
| `entity` | entities | `true`. |
| `aliases` | entities | The ids this entity is also known by: the ids it retired first (ordinally sorted), then the legacy `$ClassName` model ids its controller publishes. Always present, possibly empty. |
| `key` | entities, keyed item types | The property paths that identify an instance. |
| `display` | entities | The property that names an instance: the first of `name`, `matchCode`, `number` the entity has as a string. Absent when it has none. |
| `extendable` | entities | Whether the entity accepts an organisation's declared addon fields. |
| `queryable` | entities | `true`: every entity in the pool is accepted as a query's entity type. |
| `notFilterable` | entities | The scalar paths the entity refuses to filter on: members the service returns and does not store, which the query engine refuses with `NOT_STORED`. Ordinally sorted. Always present, possibly empty. |
| `notSortable` | entities | Paths the entity refuses to sort on. Always present; empty in this version: what makes a stored scalar unsortable, crossing a collection, is visible in the descriptors. |
| `operations` | entities | The REST operations by slot. Absent when no controller is linked. |
| `items` | entities | The item collections under the entity. Always present, possibly empty. |
| `properties` | object entries | The property list. Absent on an enum entry; an object entry always carries it, empty included. |

`key` is present when the type declares an identity, a stored document (`IDocument<T>`) or an
embedded item (`IItemId`), and its property list carries the `id` property. A plain value object
has no key, and an array of one is a value list, not an item collection.

**An enum entry has no `properties` member at all**, and an object entry has no `kind`. A
consumer resolving a pointer tells the two apart by that.

### 1.6 Property descriptor

Exactly one property list per type, describing the query shape. Members, in order:

| member | meaning |
|---|---|
| `name` | The camelCase wire name. Absent on a nested descriptor. |
| `storageName` | The name the member is stored and queried under, present only where it is not `name` with its first letter upper-cased. |
| `kind` | One of the kinds below. |
| `type` | A pointer into the pool. Present on `object` and `enum`. |
| `of` | The element descriptor of an `array`. |
| `value` | The value descriptor of a `dictionary`. |
| `nullable` | Whether a client can read null out of the member. Absent on a nested descriptor. |
| `displayName` | The human label, present only where it is not the de-camelCased `name`. |
| `description` | A description. Reserved: nothing populates it in this version. |
| `snapshotOf` | The entity this member is an embedded copy of. Travels with the pointer, so it appears on nested descriptors too. |
| `references` | The foreign key this member is: `{ entity, field, joinable, inferred }`. |
| `constraints` | `{ maxLength, min, max, pattern }`. Reserved: nothing populates it in this version. Bounds are strings. |
| `deprecated` | `{ since, replacedBy, note }`. Reserved: nothing populates it in this version. |

A nested descriptor (an array's `of`, a dictionary's `value`) describes a shape, not a member:
it carries `kind`, `type`, `of`, `value` and `snapshotOf` only.

**`storageName` and `displayName` mark where the derivation from `name` is wrong.** Both are
absent in the ordinary case, and absence means "derive it". camelCasing is lossy over an acronym
run: `QRCode` is served as `qrCode`, from which a consumer derives the storage name `QrCode`
(which matches no rows) and the label "Qr Code". On such a member the document publishes
`"storageName": "QRCode"` and `"displayName": "QR Code"`. A query is written in **wire** spelling
at every depth; the storage name is published for consumers that address storage themselves, and
the query engine refuses a storage-spelled path outside compatibility mode.

The kind vocabulary is **closed within a major version**: a reader may refuse a document
carrying a kind it does not know, so a new kind is a major bump and `unknown` is the escape hatch
for anything the model cannot describe.

### 1.7 Kinds and wire encoding

Scalar: `string` · `int` `long` `decimal` `double` · `bool` · `guid` · `date` `dateTime`
`timeSpan` · `enum` · `binary` · `unknown`. Composite: `object` (with `type`), `array` (with
`of`), `dictionary` (with `value`).

| kind | in a query result |
|---|---|
| `int` `double` | JSON number |
| `long` `decimal` | JSON string |
| `guid` | string |
| `date` | `YYYY-MM-DD` |
| `dateTime` | ISO-8601 UTC |
| `timeSpan` | ISO-8601 duration |
| `enum` | JSON number |
| `binary` | base64 |

This table describes the values a service *returns*. A filter operand of the query endpoint is
encoded differently; that is the query endpoint's request contract, not this document's.

`unknown` is explicit: a member the document cannot describe (an untyped bag, a serializer's own
container, a declared `object`, a collection with no element type) says so rather than degrading
to `object`. The CLR mapping: `sbyte`, `byte`, `short`, `ushort` and `int` are `int`; `uint`,
`long` and `ulong` are `long`; `float` and `double` are `double`; `char` and `Uri` are `string`;
`DateOnly` is `date`; `DateTime` and `DateTimeOffset` are `dateTime`; `byte[]` is `binary`;
`TimeOnly` and `object` are `unknown`.

### 1.8 Enums

The value list lives on the pooled enum entry; a property that uses the enum carries `kind` and
a pointer only.

```jsonc
"t_transactionConvertState": {
  "kind": "enum", "flags": false,
  "values": [ { "name": "NotConverted", "value": 0, "active": true },
              { "name": "Closed",       "value": 1, "active": false } ]
}
```

`name` is the CLR member name verbatim, the one string in the document that is not camelCased.
`value` is a JSON number; **a reader must accept a JSON string too**, so a value above 2⁵³ can
be published exactly without a format change. `values` is in declaration order. `active: false`
retires a member (the CLR `[Obsolete]`) without breaking historical data. Generated enum types
must be open: adding a value is not a safe change for a closed consumer. A nullable enum is the
same enum with `nullable: true` on the property.

### 1.9 What an absent member means

| absence reads as | members |
|---|---|
| a default the reader substitutes | `flags` (`false`) · `active` (`true`) · `inferred` (`false`) |
| derive it from `name` | `displayName` · `storageName` |
| does not apply to this descriptor | every entity-only member on a structural entry; every member-only member on a nested descriptor; `values`/`flags` outside an enum; `properties` on an enum; `of` outside an array; `value` outside a dictionary; `type` outside `object`/`enum` |
| unknown, and no default is safe | `nullable` on a nested descriptor · `references.field` where the target's key cannot be resolved · `description` |
| there is none, stated by an empty list instead | `aliases` `notFilterable` `notSortable` `items` on an entity |
| a definite negative | `display` (nothing names an instance) · `operations` (no controller linked) · `snapshotOf` (not a copy) · `references` (not a foreign key) · `diagnostics` (the build was clean) |

An absent `nullable` on a nested descriptor is not `false`; the annotation at that depth is
unreliable, so the document says nothing. An absent `displayName` is not "no label".

### 1.10 Relationships

```jsonc
// embedded, owned by the parent — never joinable
{ "name": "loadingSlots", "kind": "array", "of": { "kind": "object", "type": "#/types/t_loadingSlot" } }

// embedded copy of an entity owned elsewhere — never joinable
{ "name": "status", "kind": "object", "type": "#/types/vehicle.status", "snapshotOf": "vehicle.status" }

// foreign key
{ "name": "employeeId", "kind": "guid",
  "references": { "entity": "hr.employee", "field": "id", "joinable": false, "inferred": true } }
```

A pointer whose target is an entity entry is a snapshot by construction: an entity is a
top-level document, so an instance of one inside another document is a copy of that row.

`references` is emitted on a guid property (an array of guids included) that **declares** a
target: `[OxQLReference]` on the id member, or `[ReferenceId]` on the navigation property that
names it. Name inference is gone, so `inferred` is `false` on every reference; the member is kept
because removing it is a format break. `field` is the member of the target the value matches, and
defaults to the target's key. `joinable` is `true`: a declared reference is exactly what makes a
`lookup` or a `resolve` legal, and one that is not declared is refused with `LOOKUP_NOT_DECLARED`
or `RESOLVE_NOT_DECLARED`.

### 1.11 Paths

```
path    := segment ( "." segment )*
segment := [a-z][a-zA-Z0-9_]*
```

Array traversal is implicit and a path never contains an index: `loadingSlots.name` is the name
of some element. A `dictionary` segment ends the described part of a path; everything after it
is a tenant-controlled key, verbatim, not validated and not a path.

### 1.12 Items

```jsonc
"items": [
  { "path": "items",                    "aliases": ["$ShipmentModel.$ShipmentItemModel"] },
  { "path": "billingLines.costCenters", "aliases": [] }
]
```

One entry per path under the entity whose terminal property is an array of an object entry that
carries a non-empty `key` and is not an entity. Recursive, depth-first over the property list.
An entity pointer is a boundary in both directions; a dictionary is not traversed. The aliases
are the two-part legacy model ids (`$Parent.$Child`) the service's own `/ModelDefinition`
document publishes for the same collection, resolved by splitting at the first dot; a legacy id
two paths claim is given to neither.

### 1.13 Operations

```jsonc
"operations": {
  "create":  { "method": "POST",   "route": "/Vehicle" },
  "delete":  { "method": "DELETE", "route": "/Vehicle/{id}" },
  "get":     { "method": "GET",    "route": "/Vehicle/{id}" },
  "replace": { "method": "PUT",    "route": "/Vehicle/{id}" },
  "update":  { "method": "PATCH",  "route": "/Vehicle/{id}" }
}
```

`method` is a real HTTP verb, upper-case. `route` is app-relative below the service's API base
path, route parameters left as templates. The map is open; the five slots are selected from the
linked controller's routing: `get` is `GET` with a template that is exactly one route parameter,
`create` is a bare `POST`, `update` is `PATCH`, `replace` is `PUT` and `delete` is `DELETE`, each
with a single-parameter template. `PATCH` and `PUT` never collapse into one slot. Every other
action is not an entity operation. No request or response shapes are published; the service's
OpenAPI document types them.

### 1.14 Diagnostics

```jsonc
"diagnostics": [
  { "code": "duplicate-entity-id", "target": "probe.twin",
    "detail": "2 declarations claim this id, so none of them is described." }
]
```

Published only for findings a client could not detect from absence: an entity dropped for an
ambiguous id (`duplicate-entity-id`), a pointer with no target (`dangling-type-pointer`), and a
pool that is empty because the scan, or the build after it, threw (`entity-scan-failed`) or
because the host named no assemblies (`entity-assemblies-missing`). `target` is in wire terms; nothing names a CLR type.
The array is inside the revision hash. Every other finding is logged at startup only (section 3.3).

### 1.15 Compatibility

- `schemaVersion` is `<major>.<minor>`. A **minor** bump is an additive change, and a consumer
  built for `1.0` must not refuse a `1.x` document. A **major** bump means a consumer could read
  the document wrong, and a consumer is entitled to refuse it.
- Adding a member, an enum value or a diagnostic code is additive. Adding an enum value is not
  safe for a closed consumer, which is why generated enums are open.
- Removing or renaming a member, moving a diagnostic code between the refusing and the published
  set, or changing what a member means is a major bump.
- Promoting a structural type to an entity is a semantic break even though no type changes: the
  pointing property gains `snapshotOf`, and a value that was the parent's own data becomes a
  copy that can be stale.

Out of scope in this version: writes — the document describes read shapes only.

Addon keys are per organisation, so they are not in the document, but they are no longer
undescribed: `GET /schema/addons` returns the calling organisation's definitions per entity in
this same descriptor format, and a consumer merges them under the entity's `addon` member. A
defined key is typed and filterable; an undefined one stays opaque.

---

## 2 · What a service declares

An entity is a class carrying the query engine's `[OxQLType("<service>.<entity>", "<collection>")]`.
The id is the entity's stable identifier and must be `<service>.<entity>` in lower-case segments.
Everything reachable from an entity through public instance properties is pooled automatically;
nothing else is declared.

| you want | you declare |
|---|---|
| an entity | `[OxQLType]` on the class; `Extendable = true` publishes `extendable` |
| the entity's REST operations and legacy aliases | list its controller in `ConfigureModelDefinitions()`; the controller is linked to the entity whose response DTO carries `[SearchKey("<entity id>")]`, or whose name is `<Entity>Model` / `<Entity>Response` among that controller's declared responses |
| a foreign key | `[OxQLReference("<entity id>")]` on the id member, or `[ReferenceId("<id property>")]` on the navigation property, whose type is the target entity; nothing is inferred from a name |
| a retired id, after renaming an entity's id | override `ConfigureOxSchema` in `Startup` (section 2.1) |
| a key on an embedded item type | implement `IItemId` |

### 2.1 Retiring an entity id

Renaming an entity's `[OxQLType]` id breaks every persisted configuration that holds the old
one, so the old id is published as an alias for as long as such configurations exist:

```csharp
protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema)
{
    schema.RetireEntityId("vehicle.department", "department");
}
```

The retired id appears first in the entity's `aliases`. The query engine answers it as the
current entity and says so with an `ENTITY_ID_RETIRED` diagnostic carrying `params.currentId`, so
a stored configuration keeps working while it is migrated. The legacy `$ClassName` model ids in
the same list are not queryable; they are for the configuration resolvers only.

---

## 3 · How the document is built

`AddOxSchema` (called by `Bootstrap`) registers `OxSchemaRegistry` as a singleton, hands the
registry's entity model to the query engine as its `IEntityModelProvider`, and installs a
startup filter that resolves the registry before the first request. The build runs once, on the
startup thread, and is one pass in `OxSchemaBuilder`:

1. **Legacy document.** `/ModelDefinition` is generated from the declared controllers by the
   same generator as before and held beside the schema; the schema reads its published model
   ids for `items`. A controller the generator cannot describe is dropped and logged; the
   others are served.
2. **Entity model.** The query engine's model builder (`ClrModelBuilder` in `OxQL.Model`) scans
   the declared assemblies for `[OxQLType]`, drops every claimant of a duplicated id, and
   describes each entity through the MongoDB driver's serializer registry: wire names, storage
   names, whether a member is stored at all, nullability, declared references and snapshots.
   Every type reached is pooled once, and the structural types get their `t_` ids there, tails
   included. The model's findings become the document's.
3. **Projection.** `TypePoolWalker` turns the model's pool into pool entries under the model's
   ids, members in the model's order. Nothing is walked a second time: the engine binds against
   this model, and the document is its wire view.
4. **Entity metadata.** Per entity: the label, the key (the model's), the display property, the
   aliases (the retired ids, then the legacy ids of the linked controller), `extendable`, the
   unstored scalar paths as `notFilterable`, and the operations read off the linked controller.
5. **Item collections**, over the finished pool.
6. **Validation.** Id grammar, property-name grammar and pointer integrity, over the finished
   pool.
7. **Posture, serialisation, revision.** See section 3.3. The document is serialised canonically
   once for the revision and once for the body.

Because step 2 looks up a serializer for every reachable type, the build creates and freezes
the MongoDB class map of each of them. Section 5.2 says what that asks of a service.

Nothing is written to disk. `OxSchemaRegistry` exposes the entity model, the document, the body,
the revision, the entity tag, every finding, the legacy document and whether `/schema` requires
authorization. Three controllers read from it: `SchemaController`, `ModelDefinitionController`,
and `AddonDefinitionController`, which checks an entity id against the model. The actions behind
`GET /schema` and `GET /ModelDefinition` are synchronous and take a cancellation token they
never await, because they serve bytes built at startup; `GET /schema/addons` reads the calling
organisation's definitions per request, through a cache.

### 3.1 Layout

```
OxSchema/
  AddonDescriptors.cs  the body of GET /schema/addons: an organisation's definitions as descriptors
  Document/   the wire contract as immutable records, one file per section, no dependency on the rest
  Build/      model → document: options, entity discovery, the pool projection, entity metadata,
              controller link, item collections, the descriptor visitor, the validator, the
              findings and their codes
  Legacy/     the frozen /ModelDefinition document
  Hosting/    the registry singleton, AddOxSchema, the options builder, the startup logger, the
              authorization filter of GET /schema
OxQL/         the host's side of the query engine: the organisation scope provider, the internal
              batch route, the remote query client, the addon definition rules, source and cache
Controller/   SchemaController (GET /schema, GET /schema/addons),
              ModelDefinitionController (GET /ModelDefinition)
Controllers/  AddonDefinitionController (the /AddonDefinition operations)
```

### 3.2 Byte rules the code keeps

- Member order of every record is explicit, because it is inside the revision.
- Property order is the model's member order: the most derived type first, then each base type,
  declaration order within a type. The engine's model builder reads it from the metadata token,
  not from reflection's own order, and the projection keeps the order it is given.
- Every map is ordinally sorted; every list is in generator order.
- The canonical serializer options, escaper included, are inside the revision.
- The legacy document is serialised with CRLF line endings on every platform.

### 3.3 How the build fails

Every defect the build meets is a **finding** with a code, a wire-term target, a publishable
sentence, and, where useful, the CLR names that make the log line actionable. A finding has two
independent costs:

| code | refuses | published |
|---|---|---|
| `duplicate-entity-id` | yes | yes |
| `dangling-type-pointer` | yes | yes |
| `entity-scan-failed` | no | yes |
| `entity-assemblies-missing` | no | yes |
| `entity-id-off-grammar`, `structural-id-off-grammar`, `property-name-off-grammar` | no | no |
| `controller-link-ambiguous`, `reference-declaration-unresolved`, `collection-untyped`, `entity-type-shared` | no | no |

**Refusing** is for ambiguity, where no reading of the document is correct. A host **fails
fast** on a refusing finding in the `Development` and `Local` environments and under continuous
integration (the `CI` environment variable, or `TF_BUILD`, which Azure Pipelines sets). The
check runs when the host starts, not when it is compiled: a developer meets the defect on the
first local start, and a pipeline meets it only where it starts the host, for example in an
integration test. Everywhere else the host logs the findings and serves the document, with the
published ones in `diagnostics`, so a metadata defect cannot take a running service down.

The same holds for a build that throws instead of reporting a finding. A fail-fast host lets
the exception stop the start. Every other host logs it at `Critical` and serves a document
without types that carries `entity-scan-failed`; the query engine then knows no entities, the
legacy document is still served where it can be generated, and every other route of the service
is unaffected.

**Published** findings are the ones a client could not detect from absence. Every other finding
is logged at startup and never reaches the wire: publishing it would make consumers refuse a
document that is complete. Both sets are closed and keyed on the code.

The startup log carries one summary line, one line per finding, and one line per controller the
legacy generator could not describe.

---

## 4 · Configuration

Everything below is read from the host's configuration (`appsettings.json`, environment
variables) when the host starts. A host that sets none of it keeps the defaults named here.

### 4.1 Who may read `/schema`

| key | type | default |
|---|---|---|
| `OxSchema:RequireAuthorization` | bool | `false` |

`GET /schema` is anonymous by default, the same posture as `GET /ModelDefinition`: the document
is organisation-independent, and build tooling and client generators fetch it without
credentials. What it discloses is the persisted shape of every entity: property names and kinds,
storage names where they differ from the wire name, enum members, keys, declared references, the
REST routes of the linked controllers and the query limits. It carries no CLR namespace or
assembly name, no data, and nothing about an organisation. It does describe the *stored* entity,
where `/ModelDefinition` describes response models and honours `[InternalProperty]`; no attribute
keeps a member out of the schema.

With the option on, `GET /schema` applies the host's default authorization policy (a bearer
token or an API key) and refuses every other caller the way `GET /schema/addons` does. The bytes
an admitted caller gets are the same. A consumer that fetched the document without credentials
has to send them then; the OxQL Studio already sends its token with the schema request.
`GET /schema/addons` always requires an authenticated caller with an organisation, and
`GET /ModelDefinition` stays anonymous either way. A service can also set the option in code,
`schema.RequireAuthorization = true` in `ConfigureOxSchema`, which runs after the configuration
is read and therefore wins.

### 4.2 Resolving into another service

A `resolve` stage, or a condition on a referenced entity another service owns, makes the query
engine call that owner: `POST http://{host}/{service}-api/{version}/internal/oxql/batch`, where
`service` is the namespace of the target entity id (`vehicle` of `vehicle.vehicle`).

| key | meaning | default |
|---|---|---|
| `InternalHosts:<service>` | The owner's host and port, the same section the internal client reads. | none |
| `InternalApiVersions:<service>` | The api version segment the owner answers on. | `v1` |
| `Auth:InternalApiKey` | The key every internal route of the cluster admits, sent on the call and checked by the owner. | a random value per process |

- **`InternalHosts`.** A declared reference into a service without an entry is logged as an
  error when the host starts, and stops the start in `Development`, `Local` and under the `CI`
  environment variable. The check reads the configuration only; nothing is called at startup. An
  entry that does not form an address is treated as an owner that cannot be reached.
- **`InternalApiVersions`.** An owner that answers on another version than `v1` needs an entry
  on every *calling* service (`InternalApiVersions__vehicle=v2`). Without it the call goes to
  `/vehicle-api/v1/…`, the owner answers 404, and the caller sees an unreachable owner: the
  resolved members are null on the page with a `RESOLVE_UNREACHABLE` diagnostic, and a condition
  on the referenced entity is refused with `RESOLVE_UNAVAILABLE`. No startup check covers this
  key. `GET /OxQL/health` lists each remotely referenced service with its reachability.
- **The internal api key.** The owner's route is `POST internal/oxql/batch`, admitted by the key
  alone like every `OxSInternalController`, and scoped by the forwarded user and organisation
  headers. Caller and owner must be configured with the same key. A key that is not configured
  is a random value, and a key configured as blank admits nobody.

### 4.3 The `OxQL` section

The query engine binds its options from the host's `OxQL` section, and `/schema` publishes the
limits of those very options. A host without the section keeps the engine's defaults, which
include the page sizes this package used to set in code (`MaxPageSize` 500, `DefaultPageSize`
100).

| key | meaning | default |
|---|---|---|
| `OxQL:Limits:*` | Every cap a request is checked against (`MaxPageSize`, `DefaultPageSize`, `MaxOffset`, `MaxBatchQueries`, …). | the engine's |
| `OxQL:Execution:MaxTimeMs`, `OxQL:Execution:ResolveTimeoutMs` | The time ceiling of one query and the budget of one call to another service. | 10000, 2000 |
| `OxQL:Compat:Enabled` | Whether a request without the contract header is answered as contract 1. | `true` |
| `OxQL:Explain:Enabled` | Whether `POST /OxQL/explain` answers, and whether the OxQL Studio offers it; 404 otherwise. | `false` |
| `OxQL:Cursor:SigningKey` | The secret paging cursors are signed with. | `Auth:Token` |

The cursor key is never used as it is: the engine derives the signing key from the secret with
HKDF-SHA256 under its own label, so reusing the auth token does not expose it. When the section
names no key the package falls back to `Auth:Token`. A host with no `Auth` section at all
starts, and answers the first OxQL request with an error, because the engine refuses to sign
with nothing; a host whose `Auth` section names no token signs with a random value per process.
Every replica of a service needs the same secret, or a cursor issued by one is refused by the
next.

Engine faults carry their message in `Development` and `local` only.

---

## 5 · Upgrading a service to this package version

### 5.1 What a service gets by bumping the package

Nothing below needs a line of code in the service.

- `GET /schema` and `GET /schema/addons` (sections 1 and 4.1), both hidden from the API explorer.
- `GET /ModelDefinition` served from memory. The document is generated by the same generator from
  the same controller list; it is built once at startup, nothing writes
  `ModelDefinition/ModelDefinition.json` any more, and the route answers `GET` only. A
  controller the generator cannot describe is left out instead of replacing the whole document.
- The query engine in version 2: the organisation scope on every entry into an entity (a
  request without an organisation is refused with 403), `POST /OxQL/batch`, resolves into other
  services, and `POST internal/oxql/batch` for the services resolving into this one (hidden from
  the API explorer). Callers written against contract 1 keep working at runtime while
  `OxQL:Compat:Enabled` is on, which is the default.
- `/AddonDefinition`: `GET {entity}`, `GET by-id/{id}`, `POST`, `PUT {id}`, `DELETE {id}`, for an
  organisation's typed addon keys. The definitions are stored in the service's own database, in
  the collection `model_definition.addon_definition`.

### 5.2 What a service has to do

- **Register MongoDB class maps and serializers while services are registered.** The schema
  build looks up the serializer of every entity and of every type reachable from one, when the
  host starts. The first lookup of a type creates its class map and freezes it. A class map
  registered in `RegisterServices` is in place by then. One registered later (in a repository's
  static constructor, a hosted service, on first use) meets a frozen map: `RegisterClassMap`
  throws, and a registration guarded by `IsClassMapRegistered` is silently skipped, which loses
  the customisation (`SetIgnoreExtraElements`, discriminators, member maps) and surfaces as a
  deserialisation error later. Move such registrations into `RegisterServices`.
- **`ConfigureModelDefinitions()` and `GetOxQLTypeAssemblies()` are called during
  `ConfigureServices`**, not from `Configure` and not only when MongoDB is configured. They must
  not depend on anything that is set up later.
- **`ModelDefinitionBuilder.AddControllerDefinitions` is `[Obsolete]`.** `Bootstrap` no longer
  calls it and nothing reads the file it writes. A service that calls it itself gets CS0618,
  which is a build break under `TreatWarningsAsErrors`; delete the call.
- **`OxQLOrganizationFilter` is removed.** `Bootstrap` registers the scope provider
  (`AddOxQLScope<OxQLScopeProvider>()`) in its place, and the engine applies the organisation at
  every entry into an entity rather than once at the root. A service that registered the filter
  itself no longer compiles; delete the registration.
- **`ModelDefinitionController` is sealed**, derives from `OxSController` and takes the
  `OxSchemaRegistry` alone. A service that subclassed or constructed it has to stop.
- **`[AuthorizeInternalApiKey]` belongs on a controller that derives from
  `OxSInternalController`.** On any other controller the call is answered with 400 and the action
  does not run. A host whose `Auth:InternalApiKey` is configured as blank admits no internal
  call at all.
- **A renamed entity id** needs `ConfigureOxSchema` (section 2.1), so configurations that
  persisted the old id keep resolving.
- **Resolving into another service** needs the keys of section 4.2 on the calling service.
- **A service that references an `OxQL.*` package itself** has to move that reference to the
  version this package references.

### 5.3 What changes in the service's swagger

The OxQL controller and `/AddonDefinition` are part of every service's OpenAPI document, so the
document changes with the bump, and so does every API client generated from it, on its next
regeneration:

- `GET /OxQL/types` is removed; `/schema` replaces it.
- `POST /OxQL/batch` is added.
- The request and result schemas of `POST /OxQL/query` and `POST /OxQL/explain` are the ones of
  contract 2, and the refusals are typed.
- The five `/AddonDefinition` operations and their request and response models are added.

A generated client changes shape only when it is regenerated. Until then a caller built on the
contract 1 shapes keeps working at runtime, through the engine's compatibility binder
(`OxQL:Compat:Enabled`).

---

## 6 · Comments in this code

A comment documents the code as it is, for a reader who has only the code: no references to
documents outside this repository, no history, no narrative of how the code came to be. A
summary line says what a member is; a remark follows only where a maintainer could otherwise
change the code into a bug, which here means a byte-stability rule, a query-engine behaviour
that is worked around, or a packaging behaviour the code depends on.
