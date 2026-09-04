# The Ox Schema

Every service built on `Simplic.OxS.Server` publishes a schema document under `GET /schema`,
below its API base path (`/vehicle-api/v2/schema`). The document describes the entities the
service's query engine accepts, every type reachable from them, their keys, relationships and
REST operations, and the limits a query has to respect. It is built once at startup from the
service's own code, held in memory, and served with a content-derived revision and entity tag.

This file is the contract of that document (**format version 1.0**), followed by what a service
declares, how the document is built, and how the build fails. The code is in
`src/Simplic.OxS.Server/OxSchema/`; the tests in `src/Simplic.OxS.Server.Test/OxSchema/`.

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
    "maxGroupFields": 20, "maxProjectionFields": 500, "regexMaxLength": 200
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
| `limits` | What a client can check a request against before sending it. Every value is the one the query engine enforces. |
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
| `notFilterable`, `notSortable` | entities | Paths the entity refuses to filter or sort on. Always present; empty in this version, which declares no exceptions. |
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
`"storageName": "QRCode"` and `"displayName": "QR Code"`. A filter path has to be written in the
storage spelling, so a consumer that ignores `storageName` gets no rows and no error.

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

`references` is emitted on a guid property (an array of guids included) when a target entity of
this document resolves: declared through `[ReferenceId]` on the navigation property that names
the id property (`inferred: false`), or by naming convention (`inferred: true`), where the wire
name minus an `Id` or `Guid` suffix equals the last segment of exactly one entity id. Nothing is
emitted where no entity resolves. `field` is the target's key when it is a single path, read
from the finished document; absent otherwise, and a reader must not substitute `id`.
`joinable` is `false` on every reference the current version publishes.

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
pool that is empty because the scan threw (`entity-scan-failed`) or because the host named no
assemblies (`entity-assemblies-missing`). `target` is in wire terms; nothing names a CLR type.
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

Out of scope in this version: writes (the document describes read shapes only), and
per-organisation declarations of addon fields (the addon bag is a `dictionary` that accepts
anything, and its keys are not paths).

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
| a foreign key the convention cannot infer | `[ReferenceId("<id property>")]` on the navigation property, whose type is the target entity |
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

The retired id appears first in the entity's `aliases`. It is an alias for configuration
resolvers, not a queryable entity type: the query engine accepts the current id only.

---

## 3 · How the document is built

`AddOxSchema` (called by `Bootstrap`) registers `OxSchemaRegistry` as a singleton and installs a
startup filter that builds it before the first request. The build runs once, on the startup
thread, and is one pass in `OxSchemaBuilder`:

1. **Legacy document.** `/ModelDefinition` is generated from the declared controllers exactly as
   before and held beside the schema; the schema reads its published model ids for `items`.
2. **Discovery.** The query engine's registry scans the declared assemblies; duplicate ids are
   read off the declarations, where the collision is still visible, and every claimant of a
   duplicated id is dropped.
3. **Walk.** Each entity's public instance properties are described, most derived type first and
   in declaration order within a type; every type reached is pooled once under a working key,
   registered before its own members are walked so cycles need no depth limit.
4. **Metadata, link, items, references.** Key, display, label and aliases per entity; the
   controller link and the operations read off it; the item collections and the reference
   fields, both over the finished pool.
5. **Structural ids.** Working keys are replaced by `t_` ids, tails assigned where a CLR name is
   shared, and every pointer rewritten through the one descriptor visitor.
6. **Validation.** Id grammar, property-name grammar and pointer integrity, over the finished
   pool.
7. **Posture, serialisation, revision.** See section 3.3. The document is serialised canonically once
   for the revision and once for the body.

Nothing is written to disk. `OxSchemaRegistry` exposes the document, the body, the revision, the
entity tag, every finding and the legacy document; the two controllers read from it. Both
controller actions are synchronous and take a cancellation token they never await, because they
serve bytes built at startup.

### 3.1 Layout

```
OxSchema/
  Document/   the wire contract as immutable records, one file per section, no dependency on the rest
  Build/      reflection → document: options, discovery, walker, metadata, controller link, items,
              relationships, structural ids, the descriptor visitor, the validator, the findings
  Legacy/     the frozen /ModelDefinition document
  Hosting/    the registry singleton, AddOxSchema, the startup logger
Controller/   SchemaController (GET /schema), ModelDefinitionController (GET /ModelDefinition)
```

### 3.2 Byte rules the code keeps

- Member order of every record is explicit, because it is inside the revision.
- Property order is the most derived type first, then each base type, declaration order within
  a type, read from the metadata token. Reflection's own order is not used.
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
integration (the `CI` environment variable), so the defect is found by building; everywhere else
the host logs the findings and serves the document, with the published ones in `diagnostics`, so
a metadata defect cannot take a running service down.

**Published** findings are the ones a client could not detect from absence. Every other finding
is logged at startup and never reaches the wire: publishing it would make consumers refuse a
document that is complete. Both sets are closed and keyed on the code.

The startup log carries one summary line, one line per finding, and one line per controller the
legacy generator could not describe.

---

## 4 · Comments in this code

A comment documents the code as it is, for a reader who has only the code: no references to
documents outside this repository, no history, no narrative of how the code came to be. A
summary line says what a member is; a remark follows only where a maintainer could otherwise
change the code into a bug, which here means a byte-stability rule, a query-engine behaviour
that is worked around, or a packaging behaviour the code depends on.
