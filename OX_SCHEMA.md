# The Ox Schema

Every service built on `Simplic.OxS.Server` publishes a schema document under `GET /schema`,
below its API base path (`/vehicle-api/v2/schema`). The document describes the entities the
service's query engine accepts, every type reachable from them, their keys, relationships and
REST operations, and the limits a query has to respect. It is built once at startup from the
service's own code, held in memory, and served with a content-derived revision and entity tag.

This file is the contract of that document (**format version 1.1**), followed by what a service
declares, how the document is built and how the build fails, how a host is configured
(section 4), and what a service gets and has to do when it upgrades to this package version
(section 5). The code is in `src/Simplic.OxS.Server/OxSchema/` and `src/Simplic.OxS.Server/OxQL/`;
the tests in `src/Simplic.OxS.Server.Test/OxSchema/` and `src/Simplic.OxS.Server.Test/OxQL/`.

---

## 1 · The document

### 1.1 Envelope

```jsonc
{
  "schemaVersion": "1.1",
  "service": "vehicle",
  "api": { "name": "vehicle-api", "version": "v2" },
  "revision": "sha256:…",
  "limits": {
    "maxPageSize": 500, "defaultPageSize": 100,
    "maxPipelineStages": 20, "maxLookupStages": 5, "maxUnwindStages": 5,
    "maxGroupFields": 40, "maxProjectionFields": 500, "regexMaxLength": 200,
    "maxOffset": 5000, "maxResolveStages": 8, "maxBatchQueries": 10, "maxLookupLimit": 100,
    "maxContinuedStages": 8, "maxFlattenDepth": 5, "maxReportPageSize": 5000
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
| `limits` | What a client can check a request against before sending it. Every value is the one the query engine enforces. The engine holds more limits than these - resolve chunking, the semi-join cap, the count cap - which a caller cannot act on in advance; `GET /oxql/health` publishes the whole set for diagnosis. Format 1.1 appends three: `maxContinuedStages` (how many stages a keyed fetch may continue at the service that owns its target), `maxFlattenDepth` (how many levels an unwind's `flatten` descends below the unwound element) and `maxReportPageSize` (the page size of a strict report request, one that names neither a cursor nor an offset; such a request may ask for the larger of this and `maxPageSize`). |
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
   generator's order; `variants` is ordinally sorted by `name`; `onlyFor` is in the model's
   order; `referenceCases` and each case's `targets` are in declaration order, because targets
   are tried in that order. For `[OxQLReferenceWhen]` attributes, declaration order is the order
   reflection returns them in: the CLR does not promise it, the C# compiler emits source order and
   reflection keeps it, so the revision is stable across builds of one source. Targets within one
   case come from one attribute's arguments and are always in source order.
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

The body is written in the content coding the caller accepts (`Accept-Encoding`): Brotli, else
gzip, with `Vary: Accept-Encoding`; without the header, or for another coding only, as it is. The
document is coded once per coding and kept, since it does not change while the host runs.
`GET /schema/addons` is coded the same way, per answer.

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
| `description` | any | The type's description, where the code declares one (section 2.3). |
| `flags` | enum entries | Whether the enum is a flags enum. |
| `values` | enum entries | The members, in declaration order: `{ name, value, active, description }`. |
| `entity` | entities | `true`. |
| `aliases` | entities | The ids this entity is also known by: the ids it retired first (ordinally sorted), then the legacy `$ClassName` model ids its controller publishes. Always present, possibly empty. |
| `key` | entities, keyed item types | The property paths that identify an instance. |
| `display` | entities | The property that names an instance: the first of `name`, `matchCode`, `number` the entity has as a string. Absent when it has none. |
| `extendable` | entities | Whether the entity accepts an organisation's declared addon fields. |
| `queryable` | entities | `true`: every entity in the pool is accepted as a query's entity type. |
| `notFilterable` | entities | The scalar paths the entity refuses to filter on: members the service returns and does not store, which the query engine refuses with `NOT_STORED`. A member with `byVariant` is not stored either and is **not** on the list: the query engine filters it (with `eq`, `neq`, `in` and `nin`). Ordinally sorted. Always present, possibly empty. |
| `notSortable` | entities | The scalar paths the entity filters on and refuses to sort on: the paths of its members with `byVariant`, which the query engine compares and can neither sort nor group by, wherever they stand. Ordinally sorted. Always present; empty for an entity without such a member: what makes a stored scalar unsortable, crossing a collection, is visible in the descriptors and not repeated here. |
| `operations` | entities | The REST operations by slot. Absent when no controller is linked. |
| `items` | entities | The item collections under the entity. Always present, possibly empty. |
| `properties` | object entries | The property list. Absent on an enum entry; an object entry always carries it, empty included. |
| `discriminator` | polymorphic object entries | `{ element, form }`: the stored element that names a value's variant (`_t` unless the class map says otherwise) and its form, `scalar` (the variant's own name) or `hierarchical` (the names from the root class down to the variant). Present exactly when `variants` is. |
| `variants` | polymorphic object entries | `{ name, type }` per concrete type a value can hold besides the type itself, ordinally by name. `name` is the value an `is` filter and an `onlyFor` list use; `type` points at the variant's own pool entry, which describes it whole. |
| `baseVariant` | polymorphic object entries | The name an `is` filter accepts for a value stored as the type itself: the type's own name when it is a concrete class. Absent on an abstract class or an interface, whose values are always one of `variants`. The names `is` accepts are `baseVariant`, where present, then every `variants[].name`. |

**Polymorphic types.** A type with registered subclasses (section 5.2) is described as its base:
its own members, then every member only some variants carry, merged in and marked with `onlyFor`
(section 1.6). An entity whose documents are stored as several subclasses is therefore rooted at
its base, not at one representative subclass. Where two variants carry one wire name with a
different kind, storage name or representation, the merged member is `unknown`. An interface with
variants is described as the union of its variants, every member marked.

```jsonc
"t_transactionItem": {
  "description": "An item of a transaction.",
  "properties": [
    { "name": "billingLineId", "kind": "guid", "nullable": true,
      "references": { "entity": "ledger.billing_line", "field": "id", "joinable": true, "inferred": false },
      "onlyFor": ["BillingLineTransactionItem"] },
    { "name": "items", "kind": "array", "of": { "kind": "object", "type": "#/types/t_transactionItem" },
      "nullable": true, "onlyFor": ["GroupTransactionItem"] }
  ],
  "discriminator": { "element": "_t", "form": "scalar" },
  "variants": [ { "name": "BillingLineTransactionItem", "type": "#/types/t_billingLineTransactionItem" },
                { "name": "GroupTransactionItem",       "type": "#/types/t_groupTransactionItem" } ],
  "baseVariant": "TransactionItem"
}
```

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
| `description` | The member's description, where the code declares one (section 2.3). |
| `snapshotOf` | The entity this member is an embedded copy of. Travels with the pointer, so it appears on nested descriptors too. |
| `references` | The foreign key this member is, when it is a **simple** one: `{ entity, field, joinable, inferred }` (section 1.10). |
| `constraints` | `{ maxLength, min, max, pattern }`, from the member's validation attributes (section 2.3). `maxLength` is a number and on strings only; `min` and `max` are strings, written in the invariant culture, because a JSON number is a double. |
| `deprecated` | `{ since, replacedBy, note }`, from `[Obsolete]`, whose message becomes `note`. |
| `values` | A closed value list `{ value, label }`. An addon descriptor of `GET /schema/addons` carries the organisation's list. In `/schema` only a member with `byVariant` carries it: the distinct values it can hold, ordinally sorted, without labels, so a reader offers them as it offers an enum's. |
| `onlyFor` | The variants of the holding type that carry this member, when not all of them do (section 1.5). A reader treats the member as absent on every other stored value. |
| `referenceCases` | Every case of a reference that is not simple, in declaration order (section 1.10). Never beside `references`. |
| `stored` | `false` on a member the service returns and does not store (`[BsonIgnore]`, computed and get-only members). No row of a query carries it, so the query engine refuses every use of it, a projection included (`NOT_STORED`), and the same holds for everything below it; the one exception is a member with `byVariant`. Absent on a stored member. |
| `storedAs` | How the value is stored where its kind does not say it, which is where a query treats it differently: `codePoint` (a `string` that is one character, stored as its code point: it compares by value and takes no `contains`, `startsWith`, `endsWith` or `regex`), `document` (a scalar stored as a document: it cannot be filtered or sorted on), `arrayOfDocuments` and `arrayOfArrays` (a `dictionary` stored as an array of entries: a collection a query can unwind, and whose values lie under one). Absent: the representation the kind implies. |

| `relation` | `{ name, member }`: the name of the relation a reader finds at this member (section 1.10, *Relation names*), derived by the query engine. On a member that carries a reference it names that reference and `member` is absent. On a member that holds an object or a collection of objects whose key member (`id`, else `referenceId`) carries a reference it names that reference, and `member` is the key member's name. Absent on every other member. |

| `byVariant` | `{ "<variant>": "<value>", … }` on a `string` member with `stored: false` of a polymorphic type: the value the member holds for each variant, by the names `variants` and `baseVariant` use, ordinally sorted. It is the model's own type member (below). Absent on every other member. |

**The model's own type member.** A polymorphic type usually says which kind a value is in a
member of its own: `type` on a tour action, on a resource. In the service that member is a
constant of each class (`public override string Type => "driver";`), which REST returns because
the serializer calls the getter and which the driver does not store because it has no setter. The
document publishes it as what it is, and says what a reader needs to use it:

```jsonc
"transport.resource": {
  "properties": [
    { "name": "type", "kind": "string", "nullable": false,
      "values": [ { "value": "car" }, { "value": "carrier" }, { "value": "container" }, { "value": "driver" },
                  { "value": "equipment" }, { "value": "tractor_unit" }, { "value": "trailer" } ],
      "stored": false,
      "byVariant": { "CarResource": "car", "CarrierResource": "carrier", "ContainerResource": "container", "DriverResource": "driver",
                     "EquipmentResource": "equipment", "TractorUnitResource": "tractor_unit", "TrailerResource": "trailer" } },
    …
  ],
  "discriminator": { "element": "_t", "form": "scalar" },
  "variants": [ { "name": "CarResource", "type": "#/types/t_carResource" }, … ]
}
```

The query engine reads the constants when it builds the model, without running a constructor: a
getter is a constant of its class when its code never reads the instance. A member qualifies when
it is a `string`, has no setter, is not stored, and is such a constant in **every** variant (and
in the type itself where the type is concrete and stored under its own name: its value is then
under `baseVariant`'s name). Several variants may hold one value. Under contract 2 the engine
writes the member into every row from the stored discriminator, and compares it with `eq`, `neq`,
`in` and `nin` by the variants that hold a value; it cannot sort, group or list values by it, and
`values` is the pick list instead. The entity's lists say the same to a reader that does not
know the member: its path is not in `notFilterable` and is in `notSortable`. A member that is a constant in some variants and reads the
instance in others gets the log-only finding `variant-constant-unresolved`, which names the
classes, and stays a plain `stored: false` member. The type's own entry carries `byVariant`; a
variant's own pool entry lists the member with `stored: false` alone. A polymorphic type that
has no such member cannot be told apart by a query's rows: add one to the model (one get-only
string property per class).

A nested descriptor (an array's `of`, a dictionary's `value`) describes a shape, not a member:
it carries `kind`, `type`, `of`, `value`, `snapshotOf` and `storedAs` only.

**What a query can do with a member is a function of this descriptor.** Whether a member can be
filtered and with which operators, sorted, grouped, unwound or followed is decided by its kind, its
leaf kind (arrays unwrapped), `stored`, `byVariant`, `storedAs`, whether its pool entry has `variants`, whether it
declares a reference, and the collections above it. The document therefore publishes no flag per
member, and the query engine's explain answer sends none either: it names each type by entity,
service and this document's `revision`, and says only what a query changes (which collections are
unwound, which rows are joined after the page). The function is written out in the engine's
`oxql-operations.md` under `POST /oxql/explain`.

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
| `long` `decimal` | JSON number, with every digit: exact on the wire. A decimal is in canonical digits (no exponent, no trailing fractional zeros). A reader that parses a JSON number into an IEEE double keeps 15 to 17 significant digits of it |
| `guid` | string |
| `date` | `YYYY-MM-DD` |
| `dateTime` | ISO-8601 UTC |
| `timeSpan` | ISO-8601 duration |
| `enum` | JSON number, also where a 64-bit integer backs the enum |
| `binary` | base64 |

This is what the services' REST API writes for the same members (`System.Text.Json`), with one
difference a reader does not see as a number: a decimal's trailing zeros are not kept. Until OxQL
2.1 a `long` and a `decimal` were JSON strings in a query result; a reader typed against that
changes its types. The query endpoint has no option for text.

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
retires a member (the CLR `[Obsolete]`) without breaking historical data. `description`, where
the enum member declares one, follows `active`. Generated enum types
must be open: adding a value is not a safe change for a closed consumer. A nullable enum is the
same enum with `nullable: true` on the property.

### 1.9 What an absent member means

| absence reads as | members |
|---|---|
| a default the reader substitutes | `flags` (`false`) · `active` (`true`) · `inferred` (`false`) · `stored` (`true`) · `storedAs` (the representation the kind implies) |
| derive it from `name` | `displayName` · `storageName` |
| does not apply to this descriptor | every entity-only member on a structural entry; every member-only member on a nested descriptor; `values`/`flags` outside an enum; `properties` on an enum; `of` outside an array; `value` outside a dictionary; `type` outside `object`/`enum` |
| unknown, and no default is safe | `nullable` on a nested descriptor · `references.field` where the target's key cannot be resolved |
| the code declares none | `description` · `constraints` (the service may still validate what it does not declare) · `deprecated` |
| there is none, stated by an empty list instead | `aliases` `notFilterable` `notSortable` `items` on an entity |
| a definite negative | `display` (nothing names an instance) · `operations` (no controller linked) · `snapshotOf` (not a copy) · `references` together with `referenceCases` (not a foreign key) · `relation` (the member neither carries a reference nor holds an object whose key member does) · `relation.member` (the name is the member's own reference's) · `discriminator` and `variants` (not polymorphic) · `baseVariant` (no value is stored as the type itself, or the type is not polymorphic) · `onlyFor` (every variant carries the member) · `keyAs` (no key conversion) · `when` (the case is unconditional) · `item` (the value names the entity itself) · `diagnostics` (the build was clean) |

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

A member is a foreign key only where the code **declares** a target (section 2): an attribute on
the id member, `[ReferenceId]` on the navigation property that names it, or a host-side
declaration (section 2.2). Nothing is inferred from a name. A declared reference is exactly what
makes a `lookup` or a `resolve` legal; one that is not declared is refused with
`LOOKUP_NOT_DECLARED` or `RESOLVE_NOT_DECLARED`.

**`references` carries simple references only**: one unconditional case with one entity target,
no item path and no key conversion, on the member the code declares it on. That is usually a guid
or an array of guids, but a string member naming a string field is simple too, and so is any
member naming a field in another service, whose kind this host cannot check. `field` is the member
of the target the value matches, and defaults to the target's key. `joinable` is `true`.
`inferred` is `false` on every reference; the member is kept because removing it is a format
break. A format 1.0 reader maps every `references` to a join on the target entity, so no other
reference ever reaches this member.

**`referenceCases` carries every other reference** (format 1.1), as a list of cases:

```jsonc
// several targets, chosen by a sibling's value; the value names an element of an item collection
{ "name": "id", "kind": "guid", "nullable": false,
  "referenceCases": [ { "when": { "path": "type", "equals": ["logistics"] },
                        "targets": [ { "entity": "transport.shipment", "item": "billingLines", "field": "id" },
                                     { "entity": "transport.tour", "item": "billingLines", "field": "id" } ] } ] }

// a string member holding a guid
{ "name": "referenceId", "kind": "string", "nullable": false,
  "referenceCases": [ { "when": { "path": "dataType", "equals": ["shipment"] }, "keyAs": "guid",
                        "targets": [ { "entity": "transport.shipment", "field": "id" } ] } ] }

// chosen by the stored variant of the object that holds the member
{ "name": "id", "kind": "guid", "nullable": false,
  "referenceCases": [ { "when": { "variant": ["DriverResource"] },
                        "targets": [ { "entity": "staff.employee", "field": "id" } ] } ] }
```

| member | meaning |
|---|---|
| `when` | The condition the case applies under; absent on an unconditional case. Either `{ path, equals }`: the sibling member `path` (wire name, a stored string or enum) holds one of `equals`, compared exactly; or `{ variant }`: the holding object is stored as one of the named variants. |
| `keyAs` | `guid`: the stored value is a string holding a guid, parsed and normalised to the target's key form. Absent: no conversion. |
| `targets` | The targets, in the order they are tried: `{ entity, item, field }`. `entity` may belong to another service. `item` is the path of the target's item collection whose element the value names; absent when the value names the entity itself. `field` is the path the value matches, on the element when `item` is present, else on the entity; always written. |

Member order: case `when`, `keyAs`, `targets`; condition `path`, `equals`, `variant`; target
`entity`, `item`, `field`. A case whose target the build cannot resolve is dropped and logged
(section 3.3); a member left with no case carries neither member.

**Relation names** (format 1.1). Every reference has a name, published as `relation`. The query
engine derives it from the model alone. A service declares nothing for it: there is no attribute
and no host call, no finding is logged for it, and nothing fails a start, a build or a schema load
because of one. A name labels a declared reference for tools (a relation list, the default alias of
a join along it); it never creates a reference, and no query names one, so stored pipelines keep
their paths.

```jsonc
// a reference member: the name lies on the member
{ "name": "billingLineId", "kind": "guid", "nullable": true, "references": { … }, "relation": { "name": "billingLine" } }

// a reference on the key member of an embedded object: the name lies on the slot that holds the object
{ "name": "sourceBillingLineReference", "kind": "object", "type": "#/types/t_sourceBillingLineReference", "nullable": true,
  "relation": { "name": "sourceBillingLine", "member": "id" } }

// the key member itself, in the pooled type, keeps the name it has where no slot stands above it
{ "name": "id", "kind": "guid", "nullable": false, "referenceCases": [ … ], "relation": { "name": "id" } }
```

The rule, by wire names:

1. A reference member whose id member a navigation property names (`[ReferenceId("StartAddressId")]`
   on `StartAddress`) takes the navigation property's name: `startAddress`.
2. A reference on the key member of an embedded object, or of the objects of a collection, is named
   at the slot, the member that holds the object or the collection: the slot's name without a
   trailing `Reference` or `Ref`. `sourceBillingLineReference.id` is `sourceBillingLine`,
   `resources[].id` is `resources`. The key member is `id`, else `referenceId`; where both carry a
   reference the slot names `id`.
3. Any other reference member is named by its own name without a trailing `Ids` (the plural `s`
   stays), `Id`, `Number` or `Key`: `tourId` is `tour`, `vehicleIds` is `vehicles`.
4. Where none of those ends the name, without a trailing `Reference` or `Ref` (`ownerRef` is
   `owner`), else the name as it is.

A suffix is stripped only when something is left, one suffix only, and the result starts in lower
case. Two members of one type that derive the same name both take their own wire name instead, and
so does a member whose derived name is the wire name another one fell back to: names are unique
among the relation names of a type. A stored member of the same name is no collision.

**Reading it.** The name of the reference at a member path is the `relation.name` of the member one
level above the path's last segment when that member's `relation.member` is the last segment (the
slot), else the `relation.name` of the path's own member. One pooled type sits in several slots
under several names, which is why the name of a key member lies on the slot. The query engine
answers the same name in explain (`aliases.<alias>.reference.name`).

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
ambiguous id (`duplicate-entity-id`), a pointer with no target (`dangling-type-pointer`: a
property's type, a snapshot source or a variant's `type`), and a
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
- Refining `unknown` into a described kind, and replacing an entity's representative subclass by
  its polymorphic base, are minor when every published member keeps its name and path. Both
  change what a consumer's generated types say about a member (an `unknown` member becomes an
  object; a member of the former subclass becomes an `onlyFor` member, optional in practice), but
  no member a 1.0 reader reads is read wrong.
- Promoting a structural type to an entity is a semantic break even though no type changes: the
  pointing property gains `snapshotOf`, and a value that was the parent's own data becomes a
  copy that can be stale.

**Format 1.1** is the first minor bump. It adds `description`, `constraints` and `deprecated`
contents (reserved and empty in 1.0), enum value `description`, `discriminator`, `variants`,
`baseVariant`, `onlyFor`, `referenceCases`, `stored`, `storedAs`, `relation`, `byVariant` (with `values` on such a member) and three limits. Two of its changes are not purely additive, and the
rule above was amended in the same change to classify them as minor rather than ship 2.0:

1. Members the 1.0 build published as `unknown` because it could not describe a polymorphic
   value are now described kinds.
2. A polymorphic entity is rooted at its base, with the members of its variants merged in under
   `onlyFor`, where 1.0 described one subclass as the entity.

Everything else only adds members, and a typed, conditional, item or multi-target reference
never enters `references`, so a 1.0 reader sees no join it would build wrong; it sees no
reference on such a member at all. Consumers:

| consumer | effect |
|---|---|
| a reader built for 1.0 | Must accept the document (minor bump) and ignore the new members. The query engine's document reader and the frontend generator `oxql-gen` both accept `1.x` and ignore members they do not know. |
| the query engine's document reader (OxQL, contract 2) | Reads every 1.1 member back into its model; `referenceCases` wins over `references`. |
| a service's generated OxQL module | Regenerated against 1.1, it may type former `unknown` members as objects and former subclass members as optional. The frontend's generator check and type check catch every call site this touches when the module is regenerated. |
| the revision | Every service's revision changes with the upgrade, since `schemaVersion` and the new limits are inside it; clients that cached the document by entity tag fetch it once again. Descriptions are inside the revision too, so editing a doc comment changes it. The limits are the engine's values, so an engine that changes a default changes the revision of every service that takes it, without a change to any model: with OxQL 2.1, `maxGroupFields` 20 → 40 (and against the published 2.0 engine `maxResolveStages` 2 → 8); a host that configures the limit publishes its own value. The engine's README lists them under *Upgrading to 2.1*. |

Out of scope in this version: writes — the document describes read shapes only.

Addon keys are per organisation, so they are not in the document, but they are no longer
undescribed: `GET /schema/addons` returns the calling organisation's definitions per entity in
this same descriptor format, and a consumer merges them under the entity's `addon` member. A
defined key is typed and filterable; an undefined one stays opaque.

---

## 2 · What a service declares

An entity is a class carrying the query engine's `[OxQLType("<service>.<entity>", "<collection>")]`.
The id is the entity's stable identifier and must be `<service>.<entity>` in lower-case segments,
`<service>` being the service's own name in lower case (`vehicle.vehicle` in the service `Vehicle`):
other services route a reference by that prefix (sections 3.3 and 4.2).
Everything reachable from an entity through public instance properties is pooled automatically;
nothing else is declared.

| you want | you declare |
|---|---|
| an entity | `[OxQLType]` on the class; `Extendable = true` publishes `extendable` |
| the entity's REST operations and legacy aliases | list its controller in `ConfigureModelDefinitions()`; the controller is linked to the entity whose response DTO carries `[SearchKey("<entity id>")]`, or whose name is `<Entity>Model` / `<Entity>Response` among that controller's declared responses |
| a foreign key | `[OxQLReference("<entity id>")]` on the id member, or `[ReferenceId("<id property>")]` on the navigation property, whose type is the target entity; nothing is inferred from a name |
| a foreign key onto an element of an item collection | `[OxQLReference("<entity id>", "<field>", Item = "<item path>")]` |
| a foreign key held in a string member | `[OxQLReference("<entity id>", "<field>", KeyAs = OxQLKeyAs.Guid)]`; without `KeyAs` a string member referencing a guid key is dropped as `reference-key-kind-mismatch` |
| a foreign key whose target depends on a sibling or on the stored variant | one `[OxQLReferenceWhen("<sibling>", "<value>", "<target>", …)]` per case (below) |
| a foreign key on a member the service cannot annotate | a host-side declaration in `ConfigureOxSchema` (section 2.2) |
| descriptions, deprecations, constraints | doc comments or attributes (section 2.3) |
| variants of a polymorphic type | register each subclass's class map in `RegisterBsonClassMaps()`, or list it in `[BsonKnownTypes]` (section 5.2) |
| a retired id, after renaming an entity's id | override `ConfigureOxSchema` in `Startup` (section 2.1) |
| a key on an embedded item type | implement `IItemId` |

`[OxQLReferenceWhen(path, equals, params targets)]` declares one case. `path` is the wire name of a
stored string or enum sibling of the member, or `OxQLReferenceWhenAttribute.Variant`
(`"$variant"`) to test the stored variant of the object holding the member; `equals` is the value
compared exactly, or the variant name. Each target is `entity` or `entity#itemPath`. `Field` names
the path the value matches and is required when a target belongs to another service; `KeyAs`
converts as above. Attributes with the same `path` on one member form one reference with several
cases. A member that combines `[OxQLReferenceWhen]` with `[OxQLReference]`, or whose cases
conflict, keeps no reference and is logged as `reference-declaration-unresolved`.

```csharp
[OxQLReference("transport.shipment", "id", Item = "billingLines")]
public Guid ShipmentBillingLineId { get; set; }

[OxQLReference("transport.shipment", "id", KeyAs = OxQLKeyAs.Guid)]
public string ShipmentId { get; set; }

[OxQLReferenceWhen("type", "logistics", "transport.shipment#billingLines", "transport.tour#billingLines", Field = "id")]
public Guid Id { get; set; }
```

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

An organisation's addon definitions survive the rename. An entity's definitions are the rows
stored under its current id and under every id it retired, read in one query, the current id
first, then the retired ids ordinally. A path stored under more than one id is read from a live
row before a retired one, and between two rows alike from the id listed first, so the current id
wins over a retired one and a retired row never hides a live one. `/AddonDefinition` accepts a
retired id on `GET {entity}` and `POST`, answers every definition under the current id, and moves
a row to the current id when it next writes it (create, revive, update, retire), unless the
current id already holds a row of that path: then the row stays under its id, so one id never
holds two rows of one path. The query engine and `GET /schema/addons` read the same union. The
row the union does not read stays in storage and is still answered by `GET by-id/{id}`.

What a retired id keeps working, and what it does not:

| caller of the retired id | answer |
|---|---|
| contract 2 `entityType` | The current entity, with the diagnostic `ENTITY_ID_RETIRED` (`params.currentId`). |
| contract 2 `lookup.from` | The same; the diagnostic names the stage. |
| a contract 1 request (no `X-OxQL-Contract` header, or `1`) | The current entity, as before the rename and without a diagnostic. |
| another service's owner call that names it | The current entity, resolved at the owner like any request; this helps only for a retired id that carried the owner's prefix, see below. |
| `/AddonDefinition`, `GET /schema/addons`, the engine's addon read | The definitions under the current id and every retired id (above). |
| `GET /schema` | The entity under its current id, the retired ids first in `aliases`. |

| a retired id does not | so |
|---|---|
| make an id without the owner's prefix reachable from another service | A reference is routed by the prefix of the id it names (section 2.4): other services declare the new id, after the owner runs it. |
| resolve a reference declaration | `[OxQLReference("<retired id>")]` and the host-side declarations are looked up among the current ids only. Change the service's own declarations with the rename. |
| publish the entity under the old id | A client keyed on the ids of `/schema` (a generated module) is regenerated; the old id is in `aliases`. |
| exist in a package version before the schema | Rename an id in the same change as the upgrade to this package, or later. |

### 2.2 Declaring a reference on a member the service does not own

An inherited `Id`, or a type from a shared package, cannot carry an attribute. The host declares
its reference in `ConfigureOxSchema` instead, keyed on the **pooled type and the wire member**:

```csharp
protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema)
{
    // unconditional; target "entity" or "entity#itemPath"; a target in another service needs its field
    schema.DeclareReference<Contact>("employeeId", "staff.employee", field: "id");

    // one case per call; calls on the same member form one reference
    schema.DeclareReferenceWhen<Resource>("id", OxSchemaOptionsBuilder.Variant, "DriverResource", "id", "staff.employee");
    schema.DeclareReferenceWhen<SourceReference>("referenceId", "dataType", "shipment", "id", OxQLKeyAs.Guid, "transport.shipment");
}
```

| method | declares |
|---|---|
| `DeclareReference<T>(wireMember, target, field = null, item = null, keyAs = None)` | An unconditional reference. `field` null means the target's key, and only for a target of this service: a target in another service needs it, because this host cannot read that entity's key; without it no reference is emitted and the build logs `reference-target-field-unknown`, so a `lookup` or `resolve` on the member is refused as undeclared. `item` is the alternative to spelling the item path into `target`. |
| `DeclareReferenceWhen<T>(wireMember, path, equals, field, params targets)` | One case, as `[OxQLReferenceWhen]` does; `path` is a sibling's wire name or `OxSchemaOptionsBuilder.Variant`. `field` is required when a target belongs to another service. It is positional before the targets: pass `null` for the targets' key rather than leaving it out, or the first target is taken for the field. A `field` spelt like a target (`entity#itemPath`) throws `ArgumentException`; a left-out field before a target without an item path cannot be told apart and binds as the field. |
| `DeclareReferenceWhen<T>(wireMember, path, equals, field, keyAs, params targets)` | The same case with a key conversion. |

A declaration applies to `T` and its variants wherever they are embedded, and never to another
type that inherits the same CLR member. It reaches the one model both `/schema` and the query
engine are built from, so the document and the engine agree. A member that gets both kinds of
call, or also carries a reference attribute, keeps no reference and is logged as
`reference-declaration-unresolved`. So is a declaration on a member `T` does not have (a
misspelt wire name) or on a type the model does not describe, which changes nothing published.
Declarations on a type and on one of its variants for the same member leave the type's member its
reference and the variant's member none, logged the same way on the variant. None of these is
refusing or published. The declarations are copied when the options are built; a later call on
the builder changes nothing.

### 2.3 Descriptions, deprecations, constraints

A type's, a member's and an enum value's `description` is taken from the first of:

1. `[OxQLDescription("…")]`;
2. `[System.ComponentModel.Description("…")]`;
3. the XML doc `<summary>`, read from `<assembly>.xml` beside the assembly (`<inheritdoc/>`
   resolved). The file exists only where the service's project sets
   `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (section 5.2).

The text is normalised deterministically: a leading "Gets or sets", "Gets" or "Represents" is
dropped and the rest capitalised; `<see cref>` becomes the simple name, `<paramref>` and `<c>`
their text, `<para>` a blank line; whitespace is collapsed; the text is cut at a word boundary to
500 characters, `…` included.

`deprecated` comes from `[Obsolete]` on a member, its message as `note`. `constraints` comes from
`[MaxLength]` or `[StringLength]` (strings only), `[Range]` and `[RegularExpression]`. None of the
three is part of the query engine's model fingerprint; all are inside the document's revision.

### 2.4 References into another service

**The owning service of an entity is the prefix of its id.** `vehicle.vehicle` belongs to the
service `vehicle`; a reference that names it is sent to `InternalHosts:vehicle`, under
`/vehicle-api/{version}/` (section 4.2). Nothing else says who owns an entity, so:

1. **The owner goes first.** Its id has to be `<service>.<entity>` with its own service name. An
   owner whose id has no prefix, or another one, renames it and retires the old id
   (section 2.1), and is deployed. Its startup log names such ids and the line to paste
   (section 3.3).
2. **Then other services declare the reference**, to the new id and with the field, which a
   target in another service always needs:
   `[OxQLReference("vehicle.vehicle", "id")]`, or `DeclareReference` (section 2.2). This host
   cannot look into the owner when it builds its model: compare the id and the field with the
   owner's `GET /schema`. A declaration without the field is dropped
   (`reference-target-field-unknown`). Once the host runs, it checks the target ids itself
   against each owner's schema document and names the ones the owner does not publish
   (section 4.2, *The check of the declared targets*).
3. **Every service a reference points into needs configuration on the calling service**, in
   every environment it runs in (the appsettings files and the cluster):

   | key | when |
   |---|---|
   | `InternalHosts:<service>` | always; without it the start logs an error, and stops in `Development`, `Local` and under continuous integration |
   | `InternalApiVersions:<service>` | when the owner does not answer on `v1` |

   The calling service logs the resulting route of each owner at start (section 4.2), and
   `OxSchemaCheck` lists the references with their owning service for a unit test (section 3.5).
4. **Owner and caller both run this package.** Only it has the internal routes; a reference into
   an owner without them is answered as an unreachable owner.

---

## 3 · How the document is built

`AddOxSchema` (called by `Bootstrap`) registers `OxSchemaRegistry` as a singleton, hands the
registry's entity model to the query engine as its `IEntityModelProvider`, and installs a
startup filter that resolves the registry before the first request. `Bootstrap` has called
`RegisterBsonClassMaps()` by then, and it creates the AutoMapper instance (and so runs
`RegisterMapperProfiles`) right before the build, whoever resolves the registry first: a class
map a service registers in either place is there when the build looks its type up (section 5.2).
The build runs once, on the startup thread, and is one pass in `OxSchemaBuilder`:

1. **Legacy document.** `/ModelDefinition` is generated from the declared controllers by the
   same generator as before and held beside the schema; the schema reads its published model
   ids for `items`. A controller the generator cannot describe is dropped and logged; the
   others are served.
2. **Entity model.** The query engine's model builder (`ClrModelBuilder` in `OxQL.Model`) scans
   the declared assemblies for `[OxQLType]`, drops every claimant of a duplicated id, and
   describes each entity through the MongoDB driver's serializer registry: wire names, storage
   names, whether a member is stored at all, nullability, declared references and snapshots,
   variants and their discriminator, descriptions, deprecations and constraints. The host-side
   reference declarations (section 2.2) are passed to the same build. Every type reached is
   pooled once, and the structural types get their `t_` ids there, tails included. The model's
   findings become the document's.
3. **Projection.** `TypePoolWalker` turns the model's pool into pool entries under the model's
   ids, members in the model's order. A member's references are split there: a simple one into
   `references`, every other into `referenceCases`. What the model knows of storage and a kind does
   not say is published there too: `stored: false`, `storedAs`, and a concrete polymorphic type's
   own name as `baseVariant`. The relation names are the model's as well (`relation`): the engine
   derives them, the projection writes them and derives nothing itself. Nothing is walked a second time: the engine
   binds against this model, and the document is its wire view.
4. **Entity metadata.** Per entity: the label, the key (the model's), the display property, the
   aliases (the retired ids, then the legacy ids of the linked controller), `extendable`, the
   unstored scalar paths as `notFilterable` (without the type members, which the engine filters),
   the type members' paths as `notSortable`, and the operations read off the linked controller.
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
organisation's definitions per request, through a cache (section 3.4).

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
              authorization filter of GET /schema, the schema check for unit tests
OxQL/         the host's side of the query engine: the organisation scope provider, the internal
              batch and explain routes, the remote query client, the addon definition rules,
              source and cache
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
- The legacy document is serialised with LF line endings on every platform.

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
| `entity-id-off-grammar`, `entity-id-foreign-service`, `service-name-no-id-prefix`, `structural-id-off-grammar`, `property-name-off-grammar` | no | no |
| `controller-link-ambiguous`, `reference-declaration-unresolved`, `collection-untyped`, `entity-type-shared` | no | no |
| `polymorphic-member-conflict`, `polymorphic-subtype-unregistered`, `variant-constant-unresolved`, `reference-key-kind-mismatch`, `reference-case-target-unknown`, `reference-item-unknown`, `reference-candidate-undeclared` | no | no |

The last row are the model's format 1.1 codes. Each marks a member the document still describes
(as `unknown`), a variant or a reference case it leaves out in a way its absence shows, so each is
logged only. `reference-candidate-undeclared` is defined by the engine and not emitted yet. The
same holds for every other code of the engine's model build that this table does not list, such
as `reference-target-unknown`, `reference-target-field-unknown` and `retired-id-ambiguous`: a code
the document does not share is never refusing and never published.

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

**What the host says of such a model.** `OxSchemaRegistry.Degraded` is true when the build threw
or its entity scan failed (`entity-scan-failed`), and false for every model the service meant to
build: one with other findings, and one without entities, as a service that declares none has.
The host hands the value to the query engine with the model, and the engine publishes it on
`GET /OxQL/health` under contract 2 as `model { entities, degraded, reason }` and answers
`status: "degraded"` while it is true, so a failed build shows where an operator looks first.

**Published** findings are the ones a client could not detect from absence. Every other finding
is logged at startup and never reaches the wire: publishing it would make consumers refuse a
document that is complete. Both sets are closed and keyed on the code.

The startup log carries one summary line, one line per finding, and one line per controller the
legacy generator could not describe. A refusing or published finding is logged as an error
(`entity-assemblies-missing` as a warning: a service without entities names no assemblies on
purpose), and so is a finding the service has to act on although the document stays readable;
every other finding is a warning. The findings a service has to act on get one more error line
per kind, after the findings, that counts them and says what to do:

| finding | the line |
|---|---|
| `polymorphic-subtype-unregistered` | `Ox schema: N stored subtypes have no MongoDB class map and are missing from the schema (…)`, then: register them in `RegisterBsonClassMaps()` or list them in `[BsonKnownTypes]` |
| `entity-id-off-grammar`, `entity-id-foreign-service` | `Ox schema: N entity ids are not <service>.<entity> with this service's name as the prefix (…)`, then: rename each id and paste the `RetireEntityId` line of its finding |

**Entity ids.** `entity-id-off-grammar` marks an id that is not `<service>.<entity>` in
`[a-z][a-z0-9_]*` segments (`booking`), `entity-id-foreign-service` an id on grammar whose
first segment is not the host's service name in lower case (`archive.page` in the service
`Register`). Either way no other service can reference the entity, because a reference is routed
by the id's prefix (section 4.2). The finding names the entity, the id it should have and the
line that keeps the present id working, ready to paste into `ConfigureOxSchema`:

```
Ox schema finding: entity-id-off-grammar booking - Not <service>.<entity> with [a-z][a-z0-9_]* segments, so no other
service can reference this entity. Declare the id 'ledger.booking' in [OxQLType], and keep this one resolving with
schema.RetireEntityId("ledger.booking", "booking"); in ConfigureOxSchema. (refuses=False published=False)
```

Neither code refuses a start, in any environment and under continuous integration: a service
whose ids predate the rule starts after a package bump as it did before, and its entities stay
queryable under the ids they have.

**A service name that can prefix no id.** Where the service name itself is not one
`[a-z][a-z0-9_]*` segment (it holds a hyphen), no id can carry it, and no rename makes an entity
of the service reachable from another one. The build says so once, as the warning
`service-name-no-id-prefix` with the service name as its target, when the service has entities;
it reports no `entity-id-foreign-service` there and writes no line that asks for a rename to the
service's name. An id off the grammar is still reported, with the form `<service>.<entity>` in
place of an id.

### 3.4 How addon definitions reach a request

The query engine asks for an organisation's definitions of every extendable entity a request
enters, and `GET /schema/addons` for those of every extendable entity. Both ask
`AddonDefinitionSource`, which answers from `AddonDefinitionCache`, one per process, keyed by
organisation and current entity id. A definition's lifetime in the cache is
`OxQL:Cache:AddonDefinitionTtlSeconds` (30 by default, at least 1).

**When a request waits for the database.** Only when the cache holds nothing it may serve:

| the cache holds | the request | the database |
|---|---|---|
| a value no older than its lifetime | is answered from memory | is not read |
| a value older than its lifetime, up to ten lifetimes and never more than 5 minutes (5 minutes at the default lifetime) | is answered from memory, with that value | is read once, in the background, in a scope of its own; the answer replaces the value. Requests that arrive while it runs get the old value and start no second read |
| nothing: the first request for that entity and organisation in this process, the first after a write on this instance, an entry that left a full cache | waits for the read | is read once. Requests that miss together wait for the same read |
| a value older than that (nobody asked for it that long) | waits for the read, as if nothing were held | is read once |

**The bound is ten times the configured lifetime, capped at five minutes.** A value is served
until it is ten lifetimes old: 5 minutes at the default of 30 seconds, 100 seconds at a lifetime
of 10. A host that raises the lifetime does not multiply the staleness with it: ten lifetimes
above 5 minutes are cut to 5 minutes (a lifetime of 60 seconds serves for 5 minutes, not 10), and
a lifetime above 5 minutes is served for that lifetime and not a second longer, so a value past
it is always read before it is served (`AddonDefinitionCache.ServedFor`, `MaxStale`).

So after its first read an entity that is queried at least once in five minutes never costs a
request a round trip again. A background read that fails is logged, leaves the held value in
place and is not tried again for 5 seconds (or one lifetime, if that is shorter). A read a
request waits for and that fails, fails that request and the ones waiting with it; the next
request reads again. When the request that was reading is cancelled, a request waiting for its
read reads for itself.

**How old a definition can be.** For a definition written through `/AddonDefinition`:

- **On the instance that took the write**: every request that starts once the write has been
  answered reads it. The write drops the cache entry, and a read that was under way when it was
  dropped stores nothing, since it may have begun before the write.
- **On every other instance**: a request sees the write no later than one lifetime plus one
  background read after that instance last read the entity, provided requests keep arriving.
  Exactly: the instance serves the value it holds until that value is one lifetime old; the
  first request after that is still answered with it and starts the read; requests that arrive
  once the read is back (a few milliseconds) see the write. Without any request in between, the
  one request that ends the pause is answered with the old value if the pause was shorter than
  the bound above (ten lifetimes, at most 5 minutes), and waits for a fresh read if it was
  longer. No request is ever answered with a value older than that bound.
- A definition written straight into the collection, past the API, is on every instance in the
  second case.

`GET /schema/addons` and its `revision` follow the same rule, since they read the same cache.

**What the cache is bounded by.** 4 096 entries (an entity of an organisation each); beyond
that the entries asked for longest ago leave, a quarter of the capacity at a time.

**What a request's timing says.** A request that waited for the read lists it in its `timing`
as a database command, `db.list[]` with `kind: "addon"` and the collection's name
(`model_definition.addon_definition.{service}`), inside `db.ms` and outside `oursMs`; a request
that waited for a read another request had sent lists it with `shared: true`, and it counts in
neither `db.commands` nor `db.roundTrips`. `Server-Timing` then carries `addon;dur=…;desc="db"`.
A request answered from memory lists nothing. The background read belongs to no request and is
in no request's timing.

The request's cancellation token does not reach the database: the repository's read takes no
token. A request cancelled before the read never starts it; the request that sent the read
waits for it to end, as before; a request waiting for another request's read stops waiting at
once.

### 3.5 Checking the schema in a unit test

The findings of section 3.3 are written when a host starts. `OxSchemaCheck` (namespace
`Simplic.OxS.Server.OxSchema`, in the `Simplic.OxS.Server` package) builds the same schema in a
unit test of the service, without a host, a database or a broker:

```csharp
[Fact]
public void TheSchema_BuildsWithEveryVariantAndEveryReference()
{
    var result = OxSchemaCheck.Build<Startup>();

    Assert.DoesNotContain(result.Findings, f => f.Refuses);
    Assert.Empty(result.Findings.Where(f => f.Code.StartsWith("reference-")));
    Assert.Empty(result.Findings.Where(f => f.Code is "polymorphic-subtype-unregistered" or "entity-id-off-grammar" or "entity-id-foreign-service"));
    Assert.Contains(result.References, r => r.Path == "resources.vehicleId" && r.Target == "vehicle.vehicle" && r.Remote);
}
```

The test project needs a reference to the service's server project (the one that holds
`Startup`) and nothing else: the package comes with it.

| member | holds |
|---|---|
| `result.Document`, `result.Json` | The document and the body `GET /schema` answers. |
| `result.Findings` | Every finding, in the order of the startup log. `Refuses` marks the ones a `Development` host does not start on; the check itself never fails fast. |
| `result.References` | Every reference the build kept: `Entity`, `Path`, `Target`, `Item`, `Field`, `Remote`, and `Service`, the owner by the target's prefix. A declaration missing here was dropped, and a `reference-…` finding says why. |
| `result.Registry` | The registry a host would hold, with the entity model and the revision. |

The check creates the startup through its `(IConfiguration, IWebHostEnvironment)` constructor and
runs what a host runs before the build, in the host's order: the guid serializer of the base,
`RegisterBsonClassMaps()`, the mapper profiles, `GetOxQLTypeAssemblies()`,
`ConfigureModelDefinitions()` and `ConfigureOxSchema()`. It registers no service and opens no
connection.

| option | default | |
|---|---|---|
| `EnvironmentName` | `Production` | What the startup sees as its environment. |
| `Configuration` | empty | What the startup is created with; its `OxQL` section sets the published limits. With the service's own configuration the revision is the one its host publishes. |
| `RunRegisterServices` | `false` | Runs `RegisterServices` into a collection that is thrown away. For a service that registers class maps there; a service may also read configuration there that a test does not have, hence off. |

MongoDB registrations are the process's. The hook and the mapper profiles of a startup type run
once per process, however often the check is called and whether a host of that type was built
before; a registration in `RegisterServices` or `ConfigureOxSchema` has to bear a second call
itself. What the service's own code throws reaches the test as it is, and what the hook or the
profiles threw reaches every later check of the process again, without a second run. A host
that starts beside a check waits for it: the profiles run for one of them at a time.

The check registers serializers and class maps for the whole test process, and freezes the class
map of every type the schema reaches. Where other tests of the assembly register class maps or
serializers of their own, the check belongs in a test collection of its own that does not run
beside them (xunit: `[CollectionDefinition("…", DisableParallelization = true)]`), or in a test
assembly of its own. A startup that registers serializers in `RegisterServices` without a guard
bears one call with `RunRegisterServices` per process: call the check once, in a fixture the
tests share, and assert on that result.

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

A `resolve` stage, a condition on a referenced entity, or a keyed fetch whose target another
service owns makes the query engine call that owner. Every owner route is a path under the
owner's base route `http://{host}/{service}-api/{version}/`, where `service` is the namespace of
the target entity id (`vehicle` of `vehicle.vehicle`):

| route | used for |
|---|---|
| `POST internal/oxql/batch` | Executing: remote resolves, semi-joins, keyed fetches, and the stages a keyed fetch continues at the owner (at most `maxContinuedStages`). |
| `POST internal/oxql/explain` | Explaining: the origin's `POST /oxql/explain` checks the parts of a query continued at an owner there and reads from the answer which of the owner's types the aliases have (by reference: entity, service, schema revision). Same body and answer as the public explain, plus `budget` (below). |
| `GET OxQL/health?shallow=true` | Reachability, and what the owner says of itself (engine version, contract, batch cap). |

| key | meaning | default |
|---|---|---|
| `InternalHosts:<service>` | The owner's host and port, the same section the internal client reads. | none |
| `InternalApiVersions:<service>` | The api version segment the owner answers on. | `v1` |
| `Auth:InternalApiKey` | The key every internal route of the cluster admits, sent on the call and checked by the owner. | a random value per process |

- **`InternalHosts`.** A declared reference into a service without an entry is logged as an
  error when the host starts, and stops the start in `Development`, `Local` and under continuous
  integration, decided from the same value the schema build fails fast on (section 3.3: the `CI`
  or the `TF_BUILD` variable, or what `ConfigureOxSchema` states). The check reads the
  configuration only; nothing is called at startup. An entry that does not form an address is
  treated as an owner that cannot be reached.
- **`InternalApiVersions`.** An owner that answers on another version than `v1` needs an entry
  on every *calling* service (`InternalApiVersions__vehicle=v2`). Without it the call goes to
  `/vehicle-api/v1/…`, the owner answers 404, and the caller sees an unreachable owner: the
  resolved members are null on the page with a `RESOLVE_UNREACHABLE` diagnostic, and a condition
  on the referenced entity is refused with `RESOLVE_UNAVAILABLE`. Nothing is called at startup,
  so no check can tell a wrong version from a right one; instead the host logs, once at start
  and for every referenced service that has an `InternalHosts` entry, the route its calls take
  and where the version comes from:

  ```
  OxQL owner vehicle: http://vehicle-host/vehicle-api/v2/internal/oxql/batch (api version v2 from InternalApiVersions:vehicle)
  OxQL owner hr: http://hr-host/hr-api/v1/internal/oxql/batch (api version v1, the default: no InternalApiVersions:hr entry)
  ```

  `GET /OxQL/health` lists each remotely referenced service with its reachability.
- **The internal api key.** Both internal routes are `OxQLInternalController`'s, admitted by
  the key alone like every `OxSInternalController`, and scoped by the forwarded user and
  organisation headers. Caller and owner must be configured with the same key. A key that is not
  configured is a random value, and a key configured as blank admits nobody.

**The internal routes are internal calls.** The owner serves them with the same query service as
its public routes, flagged as an internal call; the route is the signal. Only there does a
request carry the keyed fetch's `keyedBy` member; the public `POST /oxql/query` and
`POST /oxql/batch` refuse it as an unknown request member. `POST internal/oxql/explain` answers
404 while the owner's explain is switched off (`OxQL:Explain:Enabled`), as the public route does;
the origin then reports those parts as unchecked.

**A body the owner cannot bind** is answered in the engine's refusal envelope on the internal
routes as on the public ones (the engine's `[OxQLRequestBody]` on both controllers): 400
`validation_error` with `INVALID_REQUEST_BODY`, or the stage's own code, and `params`. In a
batch whose entries can be told apart only the entry that does not read is refused, in its place
of `results` with `params.entry`, and the other entries run, so one wrong query of a caller does
not take the others of its batch with it. The internal key is checked first: a call without it is
401 whatever its body holds. A request under contract 1 is not touched by any of this: the host
answers a body it cannot bind with its own `problem+json`, as it always has.

**The internal explain is bounded twice.** It admits at most
`OxQL:Explain:MaxConcurrentPerCaller` (4) explains in flight per calling service, named by the
`X-OxQL-Caller` header; calls that name no service share one set of places. One more is 429 with
`Retry-After` and the refusal `EXPLAIN_LIMIT` (`params: { limit: "concurrentPerCaller", max,
retryAfter }`) before anything is bound, and the origin reports those parts as unchecked with
`complete: false`. And only there does an explain body carry `budget: { ms, calls }`: the time
and the owner calls the origin's explain has left. The owner works within them, so a chain of
owners never spends more than the origin's limits (`Explain:TimeoutMs`, `Explain:MaxOwnerCalls`);
the public `POST /oxql/explain` refuses `budget` as an unknown request member. The public route's
own limits (120 explains a minute with a burst of 30 admitted; of them 20 a minute with a burst of
5 may call owners, beyond which an explain is answered from what is kept instead of refused; 2 in
flight per user, 8 per host) are the engine's and
are listed in `GET /OxQL/health` under `limits`.

**The remote client** (`RemoteQueryClient`) sends one message per call over the named
`HttpClient` `OxQL.Remote`. It sends the internal key, the forwarded organisation, user and
correlation ids, the contract header, and `X-OxQL-Caller` with this service's name (set by
`Bootstrap`), which the owner's internal explain counts its places in flight by. The identity is
the one the engine scoped the parent query with.

- `RouteOf(service)` is the owner's base route above, or null when `InternalHosts` has no entry.
  The batch, explain and health addresses are paths under it. `ApiVersionOf(service)` is its
  version segment (the `InternalApiVersions` entry, else `v1`). The query engine reads it through
  `IRemoteOwnerInfo`, so an explain answer names it in each owner's
  `route: { apiName: "<service>-api", apiVersion }`.
- A batch's body is `queries` and `maxTimeMs` only; the owner refuses any other member with
  `UNKNOWN_REQUEST_MEMBER`. `maxTimeMs` is the owner's ceiling for the whole batch: the smaller
  positive of the engine's value and the time the call is given. The engine already writes its
  value a tenth (at most 250 ms) below the time it waits, so the owner stops before the caller
  does, and the client adds no second margin. A call without a positive budget is bounded by
  10 seconds; none waits on the HTTP client's timeout.
- The named client gives an owner two seconds to accept a connection, or what the host
  configures as `OxQL:Remote:ConnectTimeoutMs` (section 4.3; zero or less: no connect
  timeout of the client's own). An owner that accepts none fails the call then, as an
  unreachable owner (`HttpRequestException` without a status, like a refused connection), and
  does not use up the call's budget. A call on an established connection is bounded by its
  budget alone, as before: the budget elapsing is still a cancellation, which the engine reports
  as a timeout.
- `ExplainBatchAsync` posts what one round of an explain asks the owner as one request,
  `{ "checks": [ explain envelope, … ], "budget": { "ms", "calls" } }`, and returns the answers in
  the order of the checks (`{ "answers": [ … ] }`; an entry is null where the owner left a check
  unanswered). `ExplainAsync` is a batch of one check. An owner that answers anything but 200,
  cannot be reached, times out or answers another number of answers than checks makes it throw, and
  none of the checks is answered; a service with no `InternalHosts` entry is a caller error
  (`InvalidOperationException`).
- `POST internal/oxql/explain` takes that body. The owner explains the checks together: what they
  ask its own owners in a round is one call per owner, within the budget for all of them. It answers
  each check in its slim form (what an origin reads: `valid`, `errors`, the notes about the answer
  itself, `stages` with their reads and creates, `aliases`, `types`, `catalog`, `owners`,
  `revision`, `cache`, `engine`). A batch without checks, or with more than
  `OxQL:Explain:MaxBatchChecks` (64), is 400 before anything is bound. The route admits at most
  `OxQL:Explain:MaxConcurrentPerCaller` (4) calls in flight per calling service (`X-OxQL-Caller`);
  a call is one place however many checks it carries.
- An owner's refusal (any answer but 200 to a batch or an explain) is quoted in the thrown
  exception: the first error's `code` and `message` of the refusal body, else its `type` and
  `title`. The log line names the route, the service, the status and the code only. The quote is
  for diagnostics (the exception, and this host's log); the engine keeps only the HTTP status of a
  batch refusal, so the caller's answer says the owner answered with that status, never the
  owner's code or text.
- The owner's engine facts (`OwnerOf(service)`: engine version, contract, batch cap, page cap)
  are read from its shallow health answer, both when `GET /OxQL/health` probes reachability and
  when the engine asks for them before a request's first batch (`OwnerOfAsync`) while they are
  unknown or stale. That read is bounded by the health budget (2 s) and the caller's token, is
  shared by concurrent callers (one that stops waiting leaves it running for the others), and
  never fails the caller: an owner it cannot read stays unknown, and one that learned nothing is
  not read again within the time to live. Facts are kept for the health probe's time to live
  (`OxQL:Cache:HealthProbeTtlSeconds`, 10 s by default) and are unknown after it, so a
  rolled-back owner is not taken for the engine it ran before. The engine sizes each batch by the
  owner's cap and its key chunks by the owner's page from these facts, and refuses nothing by an
  owner's version: every owner an origin reaches runs this package, since only it has the internal
  routes. The client sends a batch as it is given.
- **The check of the declared targets.** The client reads which entities an owner publishes
  (`IRemoteSchemaSource`): `GET {route}/schema` of the owner, over the same route as every other
  owner call (`SchemaUrl(service)`), without a key or an identity, since the document is
  anonymous. The query engine's reference check asks for it in the background, after the host
  has started and then every few minutes (every half minute while an owner is not validated),
  and compares the target id of every declared reference with the ids the owner answers a query
  under: its entity ids and the ids they retired. A target the owner does not publish is one
  warning in the log per owner and schema revision, is listed on `GET /OxQL/health` under the
  service's `references` (`declared`, `validated`, `revision`, `unknownTargets`, `reason`) and is
  noted by explain. The read is conditional on what the client read last (`If-None-Match`), so
  an unchanged document is answered 304 without a body; what was read answers again without a
  call for the health probe's time to live (`OxQL:Cache:HealthProbeTtlSeconds`), a failed read
  included. Callers that ask for an owner while a read of it is under way share that one read.
  It is bounded by two seconds and by 16 MiB of body, and never throws. An owner that does not
  answer, answers 404 (it has no schema route, or `InternalApiVersions` names the wrong version)
  or 401 (it sets `OxSchema:RequireAuthorization`), or serves a body that is no schema document
  or a larger one leaves its references **not validated** (`reason: "unavailable"`), never wrong: nothing about the
  start or about a query depends on the check, and what was validated before an owner stopped
  answering stays.
- The engine's model provider carries the revision of the published schema document. An explain
  answer names its types by reference to the documents (entity, service, `schemaRevision`) and lists
  the revision of every service it names in `revision.schema`, so a consumer that holds another
  revision of a service's document loads it again; the answer itself carries no member.

#### One internal mechanism, two senders

An owner call is an ordinary internal call. On the wire and at the receiver nothing is OxQL's
own:

- the key is sent as `Authorization: i-api-key <Auth:InternalApiKey>`;
- the identity is the three headers every internal call carries, `UserId`, `OrganizationId`
  and `X-Correlation-ID`, filled from the same `IRequestContext`;
- the owner is found in `InternalHosts` and addressed as
  `http://{host}/{service}-api/{version}/internal/{controller}/{action}`, with `oxql` as the
  controller;
- `OxQLInternalController` derives from `OxSInternalController`, so `[AuthorizeInternalApiKey]`
  admits the call and the global `RequestContextActionFilter` restores user, organisation and
  correlation id from the headers. There is no OxQL-specific authentication.

`X-OxQL-Contract` and `X-OxQL-Caller` are added to that; neither is identity, and the caller
header only picks a limiter bucket.

Only the sending code is separate: `RemoteQueryClient` does not derive from
`InternalClientBase`, because the engine needs what that client does not offer.

| need | `InternalClientBase` | `RemoteQueryClient` |
|---|---|---|
| Owner calls of one request run in parallel. | Writes the identity into the default headers of its one `HttpClient` before each call; concurrent calls would overwrite each other's. | One `HttpRequestMessage` per call, over `IHttpClientFactory`. |
| A query is cancellable and every owner call has a time budget. | No cancellation token; the `HttpClient` default of 100 s. | The engine's token plus the call's budget (10 s without one, 2 s for health), and 2 s to connect (`OxQL:Remote:ConnectTimeoutMs`). |
| The engine turns an owner's 401, 404 or 429 into a diagnostic. | Any answer but 2xx is an `InternalClientException` without the status. | `HttpRequestException` with the status and the owner's refusal code. |
| Owner facts (engine version, caps) are cached and probes shared. | Scoped, one instance per request. | Singleton; it reads the request's identity through `IHttpContextAccessor`. |

`InternalApiVersions` is the map from a service to the version segment it answers on, with
`v1` where it has no entry. The internal client has no such map: each client class states its
version in code (`ApiVersion`). One client that reaches every owner cannot, so the map is
configuration, and today only OxQL reads it.

**Keeping them in step.** `InternalClientBase.SetRequestHeader` is the reference for the
identity headers. A change there, such as a further context header, has to be made in
`RemoteQueryClient.ForwardAsync` as well, or owner calls silently go without it. One difference
exists today: where the request has no correlation id, the internal client sends no
`X-Correlation-ID` and OxQL sends the request's trace identifier, which the receiver drops
unless it is a GUID.

### 4.3 The `OxQL` section

The query engine binds its options from the host's `OxQL` section, and `/schema` publishes the
limits of those very options. A host without the section keeps the engine's defaults, which
include the page sizes this package used to set in code (`MaxPageSize` 500, `DefaultPageSize`
100).

| key | meaning | default |
|---|---|---|
| `OxQL:Limits:*` | Every cap a request is checked against (`MaxPageSize`, `DefaultPageSize`, `MaxOffset`, `MaxBatchQueries`, …). | the engine's |
| `OxQL:Execution:MaxTimeMs`, `OxQL:Execution:ResolveTimeoutMs`, `OxQL:Execution:ChainTimeoutMs` | The time ceiling of one query, the budget of one call to another service, and the budget of a chain of calls that continue across services. | 10000, 2000, 6000 |
| `OxQL:Compat:Enabled` | Whether a request without the contract header is answered as contract 1. | `true` |
| `OxQL:Compat:Legacy:Enabled` | Whether a contract 1 request is answered by the engine's legacy pipeline. Off: the contract 2 engine answers it in contract 1 spelling and refuses what that cannot express. | `true` |
| `OxQL:Compat:Legacy:Always` | Whether every contract 1 request runs on the legacy pipeline, or only one the contract 1 spelling cannot express. | `true` |
| `OxQL:Compat:Legacy:OrganisationPath` | The stored member that holds a document's organisation. Every contract 1 read is filtered by it, in the queried collection and in every collection a `lookup` reads; a document without it is never read, so a `lookup` into a collection that lacks the member joins nothing. | `OrganizationId` |
| `OxQL:Compat:Legacy:AllowedLookupSources` | The collections a contract 1 `lookup` may read, by name. Empty: any collection of the service's database, read for the caller's organisation only. | empty |
| `OxQL:Compat:Legacy:MaxTimeMs` | The time a contract 1 request may take in the database; `OxQL:Execution:MaxTimeMs` does not apply to it. | 600000 |
| `OxQL:Explain:Enabled` | Whether `POST /OxQL/explain` and `POST internal/oxql/explain` answer, and whether the OxQL Studio console offers explain; 404 otherwise. Explain binds and compiles a request and never executes it. | `true` |
| `OxQL:Cursor:SigningKey` | The secret paging cursors are signed with. | `Auth:Token` |
| `OxQL:Remote:ConnectTimeoutMs` | Read by this package, not by the engine: how long a call to another service waits for the owner to accept a connection, in milliseconds (section 4.2). Zero or less: no connect timeout of the client's own, so a call waits for a connection as long as its budget lasts. Read once, at start; a value that is no whole number stops the start. | 2000 |

The cursor key is never used as it is: the engine derives the signing key from the secret with
HKDF-SHA256 under its own label, so reusing the auth token does not expose it. When the section
names no key the package falls back to `Auth:Token`. A host with no `Auth` section at all
starts, and answers the first OxQL request with an error, because the engine refuses to sign
with nothing; a host whose `Auth` section names no token signs with a random value per process.
Every replica of a service needs the same secret, or a cursor issued by one is refused by the
next.

A request without the `X-OxQL-Contract` header is a contract 1 request. The header takes `1` or
`2`; while `OxQL:Compat:Enabled` is on, any other value is answered 400
(`UNKNOWN_REQUEST_MEMBER`, `params.reason: "contract"`) on query, batch and explain. With the
defaults the engine's legacy pipeline answers a contract 1 request, unchanged for existing callers and with the caller's
organisation matched before any stage; each such request is logged under `OxQL.Compat.Legacy`.
The pipeline is temporary: the engine's `LEGACY.md` describes it and when it is removed.

The public explain route admits each caller `OxQL:Explain:RatePerMinute` explains a minute, in
bursts of `OxQL:Explain:RateBurst`, and answers 429 with `Retry-After` beyond that. A caller is
the user of an organisation, as the host reads them from the authenticated request (the bearer
token's or the api key's claims), so the callers of a service that all arrive from one ingress
address each have a limit of their own. A request that names neither is counted by its remote
address.

Engine faults carry their message in `Development` and `local` only.

A host with a `Monitoring` section exports the metrics of the meter named `OxQL`, beside the
service's own meters (`{service}`, `oxs-{service}`): what an engine version publishes under that
name reaches the collector without a change in the service.

### 4.4 A host without a database

A host whose configuration has no `MongoDB` section starts, and the routes that read no data
answer:

| route | answer |
|---|---|
| `GET /schema` | 200, the document: it is built from the service's model when the host starts. |
| `GET /OxQL/health`, with and without `shallow` | 200: the engine's version, contract, capabilities and limits. |
| `GET /schema/addons` | 404 as `application/problem+json`, whose `detail` says that the service is configured with no database and holds no addon definitions; 401 and 403 as on every host. |
| `POST /OxQL/query` | 500 in the engine's envelope (`internal_error`); the log names the missing section. |

The `/AddonDefinition` routes need the database. The host's authentication needs its cache
(the `Redis` section) on every route that authenticates a caller, and a host that validates its
services when it is built (`Host.CreateDefaultBuilder` does in `Development`) does not start
without that section.

---

## 5 · Upgrading a service to this package version

### 5.1 What a service gets by bumping the package

Nothing below needs a line of code in the service.

- `GET /schema` and `GET /schema/addons` (sections 1 and 4.1), both described in the service's
  OpenAPI document (section 5.3).
- `GET /ModelDefinition` served from memory. The document is generated by the same generator from
  the same controller list; it is built once at startup, nothing writes
  `ModelDefinition/ModelDefinition.json` any more, and the route answers `GET` only. A
  controller the generator cannot describe is left out instead of replacing the whole document.
- The document in format 1.1 (section 1.15): descriptions, deprecations and constraints where the
  code declares them, polymorphic types with their variants, typed and conditional references, and
  a relation name for every reference, derived by the engine (section 1.10): the service declares
  nothing for it. `GET /schema` is written in the content coding the caller accepts (section 1.3).
- The query engine with contract 2: the organisation scope on every entry into an entity (a
  request without an organisation is refused with 403), `POST /OxQL/batch`, resolves and keyed
  fetches into other services, `POST /OxQL/explain` on by default and never executing, and
  `POST internal/oxql/batch` and `POST internal/oxql/explain` for the services calling into this
  one (hidden from the API explorer, section 4.2). Callers written against contract 1 are answered
  by the engine's legacy pipeline while `OxQL:Compat:Enabled` is on, which is the default
  (section 4.3).
- `/AddonDefinition`: `GET {entity}`, `GET by-id/{id}`, `POST`, `PUT {id}`, `DELETE {id}`, for an
  organisation's typed addon keys. The definitions survive a retired entity id (section 2.1) and
  are stored in the database the service is configured with, in a collection of the service's
  own:
  - **`model_definition.addon_definition.{service}`**, the service name trimmed and lower-cased
    (`model_definition.addon_definition.vehicle`), as the scheduler names its collections
    `hangfire.{service}`. Several services are deployed onto one database; with a collection
    each, no route of one service reads, changes or retires a definition of another, also not by
    id, and two services that declare the same entity id keep separate definitions. The name is
    the host's `ServiceName`; a host that names none cannot store or read definitions: the first
    read or write of one fails (the repository itself is built, so a route that never touches a
    definition is not affected).
  - **One row per organisation, entity id and path.** The collection has the unique index
    `organization_entity_path_unique` on `{ OrganizationId: 1, Entity: 1, Path: 1 }`, partial
    over `{ IsDeleted: false }`. The package creates it itself, when a process first writes a
    definition; creating an index that exists is a no-op, so every replica asks once. If the
    server refuses to create it (the service's account has no right to create an index, rows
    already break it, an index of that name has other options) the host logs a warning for that
    collection (one, by the write that met the refusal first; one each where several writes met
    it at the same moment), goes on, and does not ask again at every write: the creation is asked
    for again an hour later (`AddonDefinitionRepository.IndexRetryAfter`), by the first write
    after that, which is how the index appears without a restart once the cause is gone. A
    command that reached no server able to answer it (the connection lost, a primary stepping
    down) is not such a refusal: nothing is concluded from it and the next write asks again.
    While the index is missing,
    uniqueness rests on the API's own read before its write, and two requests that pass that check
    together can both store their row; two such rows keep the index from ever being built until
    one is removed. What a process knows is `AddonDefinitionRepository.IndexStates`
    (`database.collection` → `{ Created, FailedAt, Reason }`); the base package has no health
    route of its own to publish it on, and the engine's `oxql/health` is the engine's. A service
    account that may create indexes on its own collections needs none of this. The API reads before it writes; of two requests
    that pass that check together the index admits one, and the other is answered `409` like
    the check would have. `PUT` and `DELETE` can answer `409` too, when the row was to move to
    its entity's current id and another request stored its path there meanwhile; sent again,
    they succeed.
  - **A retired definition keeps its row and its place in the index.** `DELETE` sets `Retired`
    and deletes nothing, so the path is still taken: `POST` for a retired path of the same kind
    revives that row instead of storing a second one, and another kind is `409`. Only rows
    marked `IsDeleted` are outside the index, and no route of the API sets that; such a row is
    outside every read as well, so it never blocks its path.
  - **Limits.** A path has at most 256 characters in at most 8 segments; a display name, a
    value and a value's label 256 characters each; a description 2 000; a value list 500 values;
    and an organisation holds at most 500 definitions that are not retired on one entity. A
    request over a limit is answered `400` (`AddonDefinitionRules`).
  - **Nothing to migrate.** No released package version ever stored definitions; the earlier
    name `model_definition.addon_definition` (without the service) existed on unreleased
    branches only. A development database that holds such a collection keeps it unread: create
    the definitions again through the API, or copy the rows of one service's entities into that
    service's collection, and drop the old one. The older `model_definition.addon_field`,
    `settings` and `contract.endpoint` are unchanged and still one collection per database.
- The OxQL Studio console at `{pathBase}/oxql` (`/vehicle-api/v2/oxql`), its assets under
  `{pathBase}/oxql/`. Every path the console is configured with (`RoutePath` and `ApiBasePath`
  `/oxql`, `SchemaBasePath` `/schema`) is relative to the path base, which the console prefixes
  itself; it calls the API beside it and reads `/schema` of the same service. Earlier versions
  configured the route with the path base spelled in, so the console answered only under a doubled
  path (`/vehicle-api/v2/vehicle-api/v2/oxql`). Its explain button follows `OxQL:Explain:Enabled`.
  The console is served while `OxQL:Studio:Enabled` is true, which defaults to true in every
  environment but `Production`: it is an anonymous developer page on the API's origin that loads
  its editor from a CDN and keeps a pasted bearer in the browser, so a production host serves it
  only when configured to (`OxQL__Studio__Enabled=true`).

### 5.2 What a service has to do

- **Register MongoDB class maps and serializers in `RegisterBsonClassMaps()`.** The schema
  build looks up the serializer of every entity and of every type reachable from one, when the
  host starts. The first lookup of a type creates its class map and freezes it.

  ```csharp
  protected override void RegisterBsonClassMaps()
  {
      BsonClassMap.RegisterClassMap<YardSlot>();
      BsonClassMap.RegisterClassMap<CoveredSlot>();
      BsonSerializer.RegisterSerializer(new DiscriminatedInterfaceSerializer<IParcelEvent>());
  }
  ```

  `Bootstrap` calls the hook once per process for a startup type, however many hosts the
  process builds from it (a test run, say), so the registrations need no guard. It runs during
  `ConfigureServices`, after the base registered its guid serializer and before
  `RegisterServices`, the mapper and the schema build.

  | where a service registers | in place for the build |
  |---|---|
  | `RegisterBsonClassMaps()` | yes; the place for it |
  | `RegisterServices`, `ConfigureOxSchema` | yes; a second host of the same process calls them again, so the registration needs a guard of its own |
  | `RegisterMapperProfiles` | yes: the mapper is created while the host starts, right before the schema is built, not on the first request that needs one. An exception thrown there stops the start in every environment |
  | a repository's static constructor, a hosted service, first use | no |

  A registration that comes after the build meets a frozen map: `RegisterClassMap` throws
  `ArgumentException: An item with the same key has already been added. Key: <type>`,
  `RegisterSerializer` throws `There is already a serializer registered for type <type>`, and a
  registration guarded by `IsClassMapRegistered` or made with `TryRegisterClassMap` is silently
  skipped, which loses the customisation (`SetIgnoreExtraElements`, discriminators, member maps)
  and surfaces as a deserialisation error later. A fault while the class maps or the mapper
  profiles are registered stops the start, in every environment, with the original exception.
  The order above keeps what a service registers in its profiles ahead of the build; the hook is
  the place that does not depend on it.
- **Register every stored subclass of a polymorphic type the same way**, or list it in
  `[BsonKnownTypes]` on the base. Only those are described as variants; a concrete subclass with no
  registered class map is left out of `variants`, its members are not merged, and the build logs
  `polymorphic-subtype-unregistered` as an error, with one line that counts the subtypes and
  names the hook (section 3.3).
- **Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>`** in the project that holds
  the entity classes if their doc comments are to become descriptions (section 2.3). Without the
  XML file beside the assembly only `[OxQLDescription]` and `[Description]` are read. The switch
  reports every public member without a doc comment as CS1591, a build break under
  `TreatWarningsAsErrors`; add `<NoWarn>$(NoWarn);1591</NoWarn>` there. Optional: a service
  without descriptions publishes a complete document.
- **Give a polymorphic type a type member, and keep it a constant.** A query tells the variants
  of a value apart by the member the model has for it: one get-only string property that every
  concrete class answers with a constant (`public override string Type => "driver";`). It needs
  no attribute and no storage: the document publishes its value per variant (`byVariant`, section
  1.6) and the query engine answers it. A getter that reads the instance, throws or returns null
  in one class takes the member out for all (`variant-constant-unresolved` in the log, with the
  class and the reason). A type without such a member has none in a query.
- **A reference on a member the service cannot annotate** needs a host-side declaration
  (section 2.2).
- **`ConfigureModelDefinitions()` and `GetOxQLTypeAssemblies()` are called during
  `ConfigureServices`**, not from `Configure` and not only when MongoDB is configured. They must
  not depend on anything that is set up later.
- **`ModelDefinitionBuilder.AddControllerDefinitions` is `[Obsolete]`.** `Bootstrap` no longer
  calls it and nothing reads the file it writes. A service that calls it itself gets CS0618,
  which is a build break under `TreatWarningsAsErrors`; delete the call.
- **`OxQLOrganizationFilter` is removed.** `Bootstrap` registers the scope provider
  (`AddOxQLScope<OxQLScopeProvider>()`) in its place, and the engine applies the organisation at
  every entry into an entity. A service that registered the filter
  itself no longer compiles; delete the registration.
- **`ModelDefinitionController` is sealed**, derives from `OxSController` and takes the
  `OxSchemaRegistry` alone. A service that subclassed or constructed it has to stop.
- **`[AuthorizeInternalApiKey]` belongs on a controller that derives from
  `OxSInternalController`.** On any other controller the call is answered with 400 and the action
  does not run. A host whose `Auth:InternalApiKey` is configured as blank admits no internal
  call at all.
- **Give every entity id the service's own name as its prefix** (`<service>.<entity>`). An id
  without it keeps working inside the service, and no other service can reference the entity;
  the startup log names each such id as an error with the line that retires it (section 3.3).
- **A renamed entity id** needs `ConfigureOxSchema` (section 2.1), so configurations that
  persisted the old id keep resolving.
- **Resolving into another service** follows section 2.4: the owner's id carries its prefix
  first, then the reference is declared, and the calling service gets the keys of section 4.2.
- **Decide two defaults per service.** `GET /schema` is anonymous unless
  `OxSchema:RequireAuthorization` is true (section 4.1), and a contract 1 `lookup` reads any
  collection of the service's database for the caller's organisation unless
  `OxQL:Compat:Legacy:AllowedLookupSources` names the collections (section 4.3).
- **A service that references an `OxQL.*` package or a sibling package of this one itself**
  has to raise that reference, or remove it (section 5.4).

### 5.3 What changes in the service's swagger

The OxQL controller and `/AddonDefinition` are part of every service's OpenAPI document, so the
document changes with the bump, and so does every API client generated from it, on its next
regeneration:

- `GET /OxQL/types` is removed; `/schema` replaces it.
- `POST /OxQL/batch` is added.
- The request and result schemas of `POST /OxQL/query` and `POST /OxQL/explain` are the ones of
  contract 2, and the refusals are typed.
- The five `/AddonDefinition` operations and their request and response models are added.

The internal routes (`internal/oxql/batch`, `internal/oxql/explain`) are hidden from the API
explorer and never appear.

A generated client changes shape only when it is regenerated. Until then a caller built on the
contract 1 shapes is answered by the engine's legacy pipeline while `OxQL:Compat:Enabled` is on
(section 4.3).

#### What a service gets in its swagger document, and what it has to do

Nothing: the package describes its own routes, and the document of the service's own
controllers is the one it was (operations, parameters, statuses, models and the document's
security requirement; a test of the package compares it).

| route | what the document says of it |
|---|---|
| `POST /OxQL/query` | The engine's summary and description, which name the address of the OxQL documentation and `GET /OxQL/health` for the limits. `X-OxQL-Contract` (`1` or `2`, optional: absent is contract 1) and `X-OxQL-Timing` as header parameters. The body `OxQLQueryBody` as `application/json`, with an example. 200 `OxQLQueryAnswer` with an example; 400, 403, 413, 422, 500 and 504 with the refusal envelope `OxQLError` (an example on 400); 401 as `application/problem+json`. |
| `POST /OxQL/batch` | The same headers; `OxQLBatchBody`; 200 `OxQLBatchAnswer`, whose example shows a page and a refusal in their places; 400, 413, 500 with `OxQLError`; 401. |
| `POST /OxQL/explain` | `X-OxQL-Contract` and `If-None-Match`; `OxQLExplainBody`; 200 `OxQLExplainAnswer` with the `ETag` header; 304; 400, 403, 413, 429 (with `Retry-After`) and 500 with `OxQLError`; 401 and 404 (explain switched off) as `application/problem+json`. |
| `GET /OxQL/health` | `shallow` as a query parameter and `X-OxQL-Contract`; 200 `OxQLHealthAnswer` with an example. No security requirement (`security: [{}]`) and no other status. |
| `GET /schema` | Summary and description with the address of this file. `If-None-Match` as an optional header parameter; 200 with the `OxSchemaDocument` model, the `ETag` header and an example; 304 without a body. No security requirement (`security: [{}]`) and no 401 or 403, unless the host sets `OxSchema:RequireAuthorization`: then the route needs what every route needs and lists both statuses. |
| `GET /schema/addons` | The same headers; 200 with an object of property descriptor lists keyed by entity id and an example; 304; 401, 403 and 404 (a host without a database, section 4.4) as `application/problem+json`. |
| `GET /AddonDefinition/{entity}` | Summary and a description that says what an addon definition is and names the address of this file, as on every `/AddonDefinition` operation. `entity` as a described path parameter; 200 with a list of `AddonDefinitionResponse` and an example; 401 and 404 as `application/problem+json`. |
| `GET /AddonDefinition/by-id/{id}` | `id` as a described path parameter; 200 `AddonDefinitionResponse` with an example; 401 and 404 as `application/problem+json`. |
| `POST /AddonDefinition` | The body `CreateAddonDefinitionRequest` as `application/json`, described, with an example; 201 `AddonDefinitionResponse` with an example; 400 and 409 with the reason as a sentence (a string, `text/plain` or `application/json`, whichever the caller accepts), 400 also as `application/problem+json` for a body the host cannot read; 401 and 403 as `application/problem+json`. |
| `PUT /AddonDefinition/{id}` | `id`; the body `UpdateAddonDefinitionRequest` with an example; 200 `AddonDefinitionResponse` with an example; 400 and 409 as on `POST`; 401 and 404 as `application/problem+json`. |
| `DELETE /AddonDefinition/{id}` | `id`; 204 without a body; 409 with the reason as a sentence; 401 and 404 as `application/problem+json`. |

- **The models are the ones of the wire.** The engine binds a request with readers of its own, so
  the document describes the bodies by the engine's documentation models (`OxQLQueryBody`,
  `OxQLStage`, `OxQLPageStage` with `count`, `OxQLQueryAnswer`, `OxQLPageInfo` with `count`,
  `OxQLError`, …) and carries none of the types the engine binds into. A member only the routes
  between services take (`keyedBy`) is in no model.
- **The query language is not in the document.** The descriptions name where it is documented
  and that `GET /OxQL/health` publishes the limits of the host; a model names the members of a
  body and their JSON kinds, and leaves what a condition or a stage means to that documentation.
- **Contract 1 is named, not offered.** The operations describe contract 2, so a client
  generated from the document is a contract 2 client: it has to send `X-OxQL-Contract: 2` with
  the bodies it is typed for, since a request without the header is read as contract 1. The
  contract 1 body and answer of the query route are in `components.schemas` as
  `OxQLContract1QueryBody` and `OxQLContract1QueryAnswer`, marked `deprecated`, and the route's
  description names them; no operation offers them as an alternative (`oneOf`), which a
  generated client would have to tell apart on every answer.
- **Security.** The authenticated query routes carry no requirement of their own: the
  document's requirement applies to them as to every controller of the service.
- **Only the statuses a route answers.** The package adds the standard `problem+json` errors
  (400, 401, 403, 404, 409, 422, 500) to every operation that does not declare them, and still
  does so for every controller of a service. Its own described routes declare each status they
  answer and get none added.
- **The addon definition routes** need what the document requires of every route. Every member
  of their models (`CreateAddonDefinitionRequest`, `UpdateAddonDefinitionRequest`,
  `AddonDefinitionValueRequest`, `AddonDefinitionResponse`, `AddonDefinitionValueResponse`) is
  described; the rules a definition is held to are in section 5.1, not in the document.
- **The models of the schema document** (`OxSchemaDocument`, `OxSchemaType`, `OxSchemaProperty`
  and the types they use) join the document's `components.schemas`. A service that has a model
  of one of those names gets no document until the two differ (the generator names both types):
  it renames its own class, or gives it another schema id. The package's own attribute does
  that, `[SwaggerSchemaId("StockProperty")]` (namespace `Simplic.OxS.Server`) on the model, once
  the service has switched it on with `services.UseCustomSwaggerId();` in `RegisterServices`. A
  service that cannot annotate the model sets the id in `RegisterServices` and leaves every
  other id to the selector in place:

  ```csharp
  services.Configure<SwaggerGenOptions>(options =>
  {
      var selector = options.SchemaGeneratorOptions.SchemaIdSelector;

      options.CustomSchemaIds(type => type == typeof(Model.OxSchemaProperty) ? "StockProperty" : selector(type));
  });
  ```
- **Comments of the query engine.** The package brings build targets
  (`build/Simplic.OxS.Server.targets`, also under `buildTransitive`) that copy
  `OxQL.AspNetCore.xml` beside the service's assemblies when it is built and when it is
  published, and the document's setup includes every `OxQL.*.xml` it finds there; one it cannot
  read is left out with a warning in the log, and the document is generated without it. A project
  reference to a project that references the package gets them too. The file is part of the
  package, beside the targets, so the document is described alike in every build, also in one
  that restores without documentation files (`NUGET_XMLDOC_MODE=skip`, the setting of the .NET
  SDK container images). A service edits nothing; one
  that wants the file left out sets `<SimplicOxSCopyQueryApiComments>false</SimplicOxSCopyQueryApiComments>`.
- **The routes between services** (`internal/oxql/batch`, `internal/oxql/explain`) stay out of
  the document in every environment. Only other services call them, with the internal key, and
  a client generated from the document must not offer them.
- **A client generated from the document** changes on its next regeneration: one operation per
  schema route is added; the query operations take the two headers as optional parameters (the
  contract as `'1' | '2'`), `If-None-Match` on explain and the schema routes, and one JSON body;
  the page stage has `count`, the page info has `count` with its state, and the errors of the
  query routes are typed as `OxQLError`. A query route lists
  the statuses it answers and no other: neither 404 nor 409 on query and batch, 200 alone on
  health. The five `/AddonDefinition` operations and their five types keep their names; each
  lists the statuses it answers and no other (no 422 and no 500, 403 on `POST` alone, 400 on
  `POST` and `PUT` alone), an answer is `application/json` only, and the error of a 400 and of
  a 409 is typed as the sentence the route answers (`string`), on a 400 beside `ProblemDetails`.

#### The type names of a regenerated client

The component names of the query routes are the ones below from this release on, so the types
of a client generated from the document carry new names once it is regenerated. The new types
describe contract 2: code that moves to them sends `X-OxQL-Contract: 2` with every request,
since a request without the header is read as contract 1 (section 4.3).

| name in a client generated before | name after regeneration |
|---|---|
| `QueryRequest` | `OxQLQueryBody` |
| `QueryResult` | `OxQLQueryAnswer` |
| `PageInfo`, `CountInfo` | `OxQLPageInfo` (`hasNextPage`, `nextCursor`, `count`) and `OxQLCount`; `totalCount` and `totalCountCapped` are members of the deprecated `OxQLContract1PageInfo` only |
| `BatchRequest`, `BatchResponse` | `OxQLBatchBody`, `OxQLBatchAnswer` |
| `ExplainRequest` | `OxQLExplainBody` (`query`, `include`, `shape`, `remote`, `catalog`), with `OxQLExplainShape` |
| `ExplainResult` | `OxQLExplainAnswer` |
| `ExplainBudget`, `ExplainCache`, `ExplainColumn`, `ExplainEngine`, `ExplainEntry`, `ExplainFilter`, `ExplainFilterPlacement`, `ExplainPlacement`, `ExplainPlan`, `ExplainRevision`, `ExplainShape`, `ExplainShapeResult`, `ExplainStage`, `ExplainUnknownValue` | none: the parts of `OxQLExplainAnswer` are objects without a component of their own, described by the explain model of the OxQL documentation |
| `Refusal`, `QueryValidationError` | `OxQLError`, `OxQLErrorDetail` |
| `Diagnostic` | `OxQLDiagnostic` |
| `PipelineStage` | `OxQLStage` |
| `PageStage` | `OxQLPageStage` (`limit`, `cursor`, `count`) |
| `LookupStage`, `ResolveStage`, `UnwindStage`, `GroupStage`, `ValuesStage` | `OxQLLookupStage`, `OxQLResolveStage`, `OxQLUnwindStage`, `OxQLGroupStage`, `OxQLValuesStage` |
| `GroupByField`, `DateTruncExpression` | `OxQLGroupKey`, `OxQLDateTrunc` |
| `MatchStage`, `ProjectStage`, `SortField`, `FilterCondition`, `FilterConditionOptions`, `AggregationExpression`, `QueryExpression`, `QueryVariables`, `ResolveBranch` | none: `match`, `project`, `sort`, a filter, the fields of a group, `variables` and `byTarget` are objects and arrays whose members the OxQL documentation describes; a filter entry of the request is `OxQLFilterEntry` |
| `KeyedByMember`, `JsonNode`, `JsonNodeOptions` | none |

New with the regeneration:

- **Two operations**, `GET /schema` and `GET /schema/addons`, and the types of the schema
  document: `OxSchemaDocument`, `OxSchemaApi`, `OxSchemaLimits`, `OxSchemaDiagnostic`,
  `OxSchemaType`, `OxSchemaProperty`, `OxSchemaEntityItem`, `OxSchemaOperation`,
  `OxSchemaEnumValue`, `OxSchemaValue`, `OxSchemaConstraints`, `OxSchemaDeprecation`,
  `OxSchemaDiscriminator`, `OxSchemaVariant`, `OxSchemaRelation`, `OxSchemaReference`,
  `OxSchemaReferenceCase`, `OxSchemaReferenceCondition` and `OxSchemaReferenceTarget`.
  `OxSchemaProperty` refers to itself (`of`, the element of a list, and `value`, the value of a
  map), so a generator has to emit a recursive type for it.
- **The answer of `GET /OxQL/health`** is typed: `OxQLHealthAnswer` with `OxQLHealthEngine`,
  `OxQLHealthService` and `OxQLHealthModel`.
- **The contract 1 shapes** `OxQLContract1QueryBody`, `OxQLContract1QueryAnswer` and
  `OxQLContract1PageInfo`, deprecated and used by no operation.
- **One media type per body.** A request body and an answer of the query routes is
  `application/json`; 401 and the 404 of explain are `application/problem+json`.

### 5.4 Packages a service references directly

`Simplic.OxS.Server` depends on its sibling packages and on the five `OxQL.*` packages at the
versions below or higher. A service that also references one of them directly, at a lower
version, does not restore: NuGet reports the downgrade as `NU1605`.

| package a service may reference directly | minimum beside this release of `Simplic.OxS.Server` |
|---|---|
| `Simplic.OxS.ModelDefinition.Service` | 3.0.1226.825 |
| `Simplic.OxS.ModelDefinition.Extension` | 3.0.826.1012 |
| `Simplic.OxS.ServiceDefinition` | 3.0.1426.1012 |
| `Simplic.OxS.ServiceDefinition.Repository` | 3.0.926.1012 |
| `OxQL.AspNetCore`, `OxQL.Core`, `OxQL.Model`, `OxQL.Mongo` | 2.1.226.1013, all four the same version |
| `OxQL.Studio` | 2.1.126.1012 |

- **Restore names one package per run.** Raise it, restore again, and repeat until the restore
  passes; a service that references two of the rows needs two rounds.
- **Or remove the direct reference.** `Simplic.OxS.Server` brings every package of the table, so
  a project that references the server package (directly or through a project reference) needs
  none of them listed. A project that does not, such as a domain project that only uses
  `[OxQLType]`, keeps its `OxQL.Core` reference and raises it.
- The other sibling packages (`Simplic.OxS`, `Simplic.OxS.Data`, `Simplic.OxS.Data.MongoDB`,
  `Simplic.OxS.InternalClient`, `Simplic.OxS.Settings*`, `Simplic.OxS.Cache*`,
  `Simplic.OxS.MessageBroker`, `Simplic.OxS.ModelDefinition`) did not change with the schema; a
  service meets them here only if its reference was older than the server package it came from.

### 5.5 Upgrading a service: the checklist

1. **Bump** `Simplic.OxS.Server` in every project that references it.
2. **Restore until it passes**: raise or remove what `NU1605` names, one package per round
   (section 5.4).
3. **Move the MongoDB class maps and serializers into `RegisterBsonClassMaps()`** (section 5.2).
   Registrations in `RegisterServices` and `RegisterMapperProfiles` keep working; the hook is
   the one place that runs once per process.
4. **Entity ids**: every `[OxQLType]` id is `<service>.<entity>` with the service's own name.
   Rename the others and retire the old id in `ConfigureOxSchema`; change the service's own
   reference declarations to the new id (section 2.1).
5. **References into other services**: the owner first, then the declaration with its field,
   then `InternalHosts:<service>` and, where needed, `InternalApiVersions:<service>` in every
   environment (section 2.4).
6. **Add the schema check** to the service's unit tests (section 3.5), so a dropped reference,
   a variant without a class map or an id off the rule fails the build of the service and not
   its first start.
7. **Start the service once and read its log**: the `Ox schema finding:` lines, the
   `Ox schema:` lines that say what to do, and one `OxQL owner` line per referenced service.
8. **Regenerate** the API clients and the OxQL modules that are generated from the service
   (section 5.3); its schema revision changes with the package.

---

## 6 · Comments in this code

A comment documents the code as it is, for a reader who has only the code: no references to
documents outside this repository, no history, no narrative of how the code came to be. A
summary line says what a member is; a remark follows only where a maintainer could otherwise
change the code into a bug, which here means a byte-stability rule, a query-engine behaviour
that is worked around, or a packaging behaviour the code depends on.
