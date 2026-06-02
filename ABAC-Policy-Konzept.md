# ABAC Policy Engine – Ausgearbeitetes Konzept

**Version:** 1.0  
**Datum:** Mai 2026  
**Status:** Ausgearbeitet Konzept

---

## 1. Überblick

Ein **Attribute-Based Access Control (ABAC)** System mit Policy-Engine für feinkörnige Zugriffskontrolle zwischen Service und Datenbank. Das System basiert auf einem **Deny-First-Ansatz** mit expliziten Allow-Rules und implizitem Deny als Standardverhalten.

### Kernprinzipien
- **Policy-orientiert**: Zentrale Verwaltung von Zugriffsregeln
- **Deny > Allow**: Deny-Policies haben höhere Priorität
- **Implizites Deny**: Standardmäßig Zugriff verweigert (Zero-Trust)
- **Allow-All-Fallback**: Optional Basis-Policy für vollständigen Zugriff
- **Redis-Caching**: Multi-Level Cache für Policies und Decisions

---

## 2. Architektur

### 2.1 Schichtenmodell

```
┌──────────────────────────────────────┐
│      Service Layer (Web API)         │
│  - Authentifizierung & Subject       │
│  - Request Handling                  │
└──────────────┬───────────────────────┘
               │
        ┌──────▼──────────────┐
        │  Policy Compiler    │
        │  - Parse JSON       │
        │  - Validate         │
        │  - Compile to       │
        │    typed AST        │
        └──────┬──────────────┘
               │
    ┌──────────▼──────────────┐
    │ Compiled Policy Cache    │
    │     (Redis)             │
    │ - Compiled Rules        │
    │ - typed AST metadata    │
    │ - Pre-built Filters     │
    │ - TTL/Invalidation      │
    └──────┬──────────────────┘
           │
    ┌──────▼─────────────┐
    │ Policy Evaluator   │
    │ - Rule Matching    │
    │ - Fast Evaluation  │
    │ - Filter Apply     │
    └──────┬─────────────┘
           │
           ├─→ Decision Cache (5 min)
           │
┌──────────▼────────────────────────┐
│ Data Access Layer (MongoDB)       │
│ - Filter-Builder                  │
│ - Query Execution                 │
└───────────────────────────────────┘
```

### 2.2 Komponenten

#### Policy Compiler
Kompiliert Raw JSON Policies zu optimierten Compiled Policies:
- Läuft als **zentraler Compile-Writer** (nicht im Request-Path der Reader-Services)
- **Parsing**: JSON → Policy/Rule Objekte
- **Validierung**: Struktur und Syntax Check
- **Compilation**: Conditions (JSON) → typsicheres AST
- **Optimierung**: Index-Hints, Filter Pre-Building (Mongo/Search)
- **Output**: Serialisierbare CompiledPolicy (für Cache)

#### Compiled Policy Cache (Redis)
- Nur kompilierte Policies cachen
- Keine serialisierten Expressions
- Pre-built Filter Definitionen (Mongo/Search)
- Kanonisches JSON der Conditions
- Invalidation bei Policy-Änderung
- TTL: 60 Minuten (or until invalidated)

#### Policy Evaluator
- Evaluiert Compiled Policies gegen Subject
- Baut in-process Matcher aus AST (kein Expression-Blob aus Redis)
- Schnelle Rule-Matching
- Nutzt pre-built Query-Artefakte aus dem Cache

---

## 3. Datenmodelle

#### Raw Policy (Input Format)
```csharp
public class Policy
{
    public string PolicyId { get; set; }
    public string Description { get; set; }
    public IList<Rule> Rules { get; set; } = [];
}
```

#### Compiled Policy (Cached Format - Redis Serializable)
```csharp
public class CompiledPolicy
{
    public string OrganizationId { get; set; }      // Organization-scoped!
    public string PolicyId { get; set; }
    public string Description { get; set; }
    public IList<CompiledRule> CompiledRules { get; set; } = [];
    public DateTime CompiledAt { get; set; }
    public string CompilerVersion { get; set; }
}

public class CompiledRule
{
    public string RuleId { get; set; }
    public string Effect { get; set; }  // "allow" | "deny"
    public string[] ActionPatterns { get; set; }
    
    // RAW Bedingungen (JSON-serialisierbar, für Laufzeit-Eval)
    public RuleConditions SubjectConditions { get; set; }
    public RuleConditions ResourceConditions { get; set; }
    
    // Pre-built MongoDB filters (serialisierbar als BsonDocument)
    public BsonDocument MongoDBFilter { get; set; }
    
    // Pre-built Search filters (serialisierbar als String/Query)
    public string SearchFilter { get; set; }
    
    // Metadata for optimization
    public RuleMetadata Metadata { get; set; }
}

public class RuleConditions
{
    // Externes, kanonisches Policy-Format (JSON-first)
    public IDictionary<string, object> RawJson { get; set; }

    // Internes, typsicheres AST für schnelle Evaluation
    public ConditionNode CompiledAst { get; set; }
}

public class ConditionNode
{
    public string NodeType { get; set; }   // "comparison" | "and" | "or"
    public string Operator { get; set; }   // "$eq", "$ne", "$in", "$gt", "$contains", etc.
    public string Field { get; set; }      // e.g. "user.role", "teams.name"
    public object Value { get; set; }      // scalar oder array
    public IList<ConditionNode> Children { get; set; } = [];
}

public class RuleMetadata
{
    public bool HasSubjectConditions { get; set; }   // null oder {} = false
    public bool HasResourceConditions { get; set; }  // null oder {} = false
    public string[] RequiredSubjectFields { get; set; }
    public string[] RequiredResourceFields { get; set; }
}
```

**Serialisierungsstrategie:**
- `SubjectConditions` & `ResourceConditions`: JSON (externes Format, wird bei Cache-Hit in `CompiledAst` kompiliert, ~0.3ms overhead)
  - Null oder `{}` = Leere Bedingung = **immer true**
- `MongoDBFilter`: BsonDocument (native serialisierbar)
- `SearchFilter`: String (trivial)
- `Func<Subject, bool>` wird **nicht** gecacht, sondern aus `CompiledAst` gebaut

#### Rule (Input)
```csharp
public class Rule
{
    public string Effect { get; set; }           // "allow" | "deny"
    public IList<string> Actions { get; set; }   // e.g. ["oxs:document:read"]
    public IDictionary<string, object> Subject { get; set; }
    public IDictionary<string, object> Resource { get; set; }
}
```

**Subject-Modell**
```csharp
Subject
├── User
│   ├── Id
│   └── Email
├── Roles (string[])
└── Teams (Team[])
    ├── Id
    └── Name
```

---

## 4. Policy-Ansatz: Deny > Allow

### 4.1 Evaluierungsprinzip

Das System folgt der Priorität:
1. **Deny-Rules werden zuerst geprüft**
2. **Bei Match: Zugriff sofort verweigert** (Kurzschluss-Evaluation)
3. **Nur bei keinem Deny-Match: Allow-Rules prüfen**
4. **Bei keinem Allow-Match: Implizites Deny**

### 4.2 Evaluierungs-Algorithmus

```
Eingabe: Subject, Action, Resource, Policies

1. Alle Deny-Rules finden, die matchen
   → if (deny_rules.count > 0) return DENY

2. Alle Allow-Rules finden, die matchen
   → if (allow_rules.count > 0) continue
   → else return DENY (implizit)

3. Resource-Filter aus Allow-Rules aufbauen
   → filter = OR(allow_rules.resource) AND NOT(OR(deny_rules.resource))

4. Abfrage mit Filter ausführen
   → return gefilterte_dokumente
```

### 4.3 Entscheidungsmatrix

| Scenario | Deny-Match | Allow-Match | Result |
|----------|-----------|-----------|--------|
| Beides | Ja | Ja | **DENY** |
| Nur Deny | Ja | Nein | **DENY** |
| Nur Allow | Nein | Ja | **ALLOW** |
| Weder noch | Nein | Nein | **DENY (implizit)** |

---

## 5. Implizites Deny – Zero-Trust Modell

### 5.1 Grundprinzip

```
Standardzustand = DENY
Zugriff nur wenn:
  (Allow-Rule matched) AND NOT (Deny-Rule matched)
```

### 5.2 Praktische Implikationen

- **Keine Policy definiert** → Zugriff verweigert
- **Nur Deny-Rules definiert** → Allow nur falls kein Deny matched
- **Action nicht in Policies genannt** → Implizites Deny
- **Subject matched keine Bedingungen** → Implizites Deny

---

## 6. Allow-All Basis-Policy

### 6.1 Zweck

Eine optionale "Weiß-Liste" Policy für Systeme, die standardmäßig vollständigen Zugriff gewähren möchten, mit spezifischen Exceptions (Deny-Rules).

### 6.2 Implementierung

```json
{
  "policyId": "allow-all-default",
  "description": "Default Allow-All Policy für Basis-Zugriff",
  "rules": [
    {
      "effect": "allow",
      "actions": ["oxs:*"],
      "subject": {},
      "resource": {}
    }
  ]
}
```

### 6.3 Kombinationsbeispiel

```
Allow-All Policy + Deny-Policies
  ↓
Allow alles, ABER:
  - Verweigere Settings für Nicht-Admins
  - Verweigere HR-Daten für externe User
  - etc.
```

---

## 7. Service ↔ Datenbank Integration

### 7.1 Architektur-Details

```
Service (z.B. ASP.NET Core)
  │
  ├─► Authentifizierung
  │   └─► Subject extrahieren
  │
  ├─► Policy Loading (mit Redis Cache)
  │   └─► Policies aus Cache/Config/DB laden
  │
  ├─► Action Mapping
  │   └─► HTTP-Request → ABAC-Action
  │       Beispiel: GET /documents → oxs:document:read
  │
  └─► Policy Engine
      │
      ├─► Regelauswertung (Subject/Action)
      │
      ├─► Wenn Deny → 403 Forbidden
      │
      ├─► Wenn Allow → MongoDB-Filter bauen
      │
      └─► Datenbank-Query mit Filter
          └─► Nur autorisierte Dokumente zurück
```

### 7.2 Implementierungs-Pattern

```csharp
// 1. Subject aus Request extrahieren
var subject = ExtractSubjectFromAuth(httpContext);

// 2. Policies laden (mit Redis Cache)
var policies = await policyService.GetPoliciesAsync(organizationId);

// 3a. Authorize-Entscheidung (für Commands/Single-Resource)
var decision = PolicyEngine.Authorize(
    subject,
    policies,
    "oxs:document:read",
    resource // optional bei list/read = null
);

if (decision == AccessDecision.Deny)
{
    return Forbidden();
}

// 3b. Query-Filter (für List/Read-Many)
var filter = PolicyEngine.BuildMongoFilter(
    subject,
    policies,
    "oxs:document:read"
);

// 4. Query mit Filter ausführen
var documents = await collection.Find(filter).ToListAsync();
```

### 7.3 Filter-Aufbau aus Policies

```
Allow-Rules:
  Rule A: tags contains "public"
  Rule B: owner == userId

Resource-Filter (Allow-Teil):
  filter_allow = (tags: "public") OR (owner: userId)

Deny-Rules:
  Rule C: status == "archived"

Resource-Filter (Deny-Teil):
  filter_deny = NOT (status: "archived")

Finale Query:
  filter = (filter_allow) AND (filter_deny)
         = ((tags: "public") OR (owner: userId)) 
           AND NOT (status: "archived")
```

### 7.4 Zwei Modi der Engine (verbindlich)

**Mode A: `Authorize` (bool Entscheidung)**  
Für Command-Operationen und Single-Resource-Zugriffe.

```text
1) Sammle Rules, die auf Action + Subject matchen
2) Wenn eine Deny-Rule (inkl. Resource, falls vorhanden) matcht → DENY
3) Sonst wenn eine Allow-Rule (inkl. Resource, falls vorhanden) matcht → ALLOW
4) Sonst → DENY (implizit)
```

**Mode B: `BuildFilter` (Query-Filter für Listen)**  
Für Read-Many (Mongo/Search/SignalR Broadcast-Filter).

```text
1) Sammle Rules, die auf Action + Subject matchen
2) allowFilter = OR(ResourceFilter aller Allow-Rules; {} zählt als true)
3) denyFilter  = OR(ResourceFilter aller Deny-Rules;  {} zählt als true)
4) finalFilter = allowFilter AND NOT(denyFilter)
5) Wenn keine Allow-Rule matched → Match-Nothing Filter (implizites Deny)
```

**Wichtig:** `Authorize` nutzt Kurzschluss-Deny-First; `BuildFilter` nutzt formale Filterkombination.

---

## 8. ABAC DSL – Bedingungssprache

### 8.1 DSL-Design

Die ABAC DSL folgt einem **JSON-first, intern typed** Ansatz:
- **Extern kanonisch:** Policies werden als JSON mit `$...`-Operatoren definiert.
- **Intern typsicher:** Das JSON wird beim Laden in ein typisiertes AST (`ConditionNode`) kompiliert.

Die DSL wird zur Laufzeit:
- Für **Subject-Evaluation** in C# evaluiert
- Für **Resource-Filtering** in die jeweilige Datenbank-Sprache kompiliert (MongoDB, SQL, etc.)

### 8.2 Unterstützte Conditions

| Operator | Beschreibung | Beispiel |
|----------|-------------|----------|
| `$eq` | Exakte Gleichheit | `"department": { "$eq": "HR" }` |
| `$ne` | Ungleichheit | `"status": { "$ne": "draft" }` |
| `$in` | Wert in Liste | `"roles": { "$in": ["admin", "owner"] }` |
| `$nin` | Wert nicht in Liste | `"roles": { "$nin": ["guest"] }` |
| `$gt` | Größer als | `"level": { "$gt": 2 }` |
| `$gte` | Größer oder gleich | `"priority": { "$gte": 5 }` |
| `$lt` | Kleiner als | `"score": { "$lt": 100 }` |
| `$lte` | Kleiner oder gleich | `"score": { "$lte": 100 }` |
| `$contains` | Liste enthält Wert | `"teams.name": { "$contains": "hr" }` |
| `$ncontains` | Liste enthält Wert nicht | `"teams.name": { "$ncontains": "guest" }` |

### 8.3 Logische Kombinatoren

**Implizites AND** (Standard – mehrere Bedingungen kombiniert)
```json
{
  "subject": {
    "teams.name": { "$eq": "hr" },
    "roles": { "$in": ["member", "editor"] }
  }
}
// Bedeutung: (teams.name == "hr") AND (roles IN ["member", "editor"])
```

**Explizites AND**
```json
{
  "subject": {
    "$and": [
      {"teams.name": { "$eq": "hr" }},
      {"roles": { "$eq": "member" }}
    ]
  }
}
```

**Explizites OR**
```json
{
  "subject": {
    "$or": [
      {"roles": { "$eq": "admin" }},
      {"roles": { "$eq": "owner" }}
    ]
  }
}
```

### 8.4 Pfad-Navigation

**Dot-Notation für verschachtelte Objekte**
```json
{
  "subject": {
    "user.email": { "$eq": "admin@example.com" },
    "teams.name": { "$eq": "hr" },
    "department.level": { "$gte": 3 }
  }
}
```

Automatische Traversierung von Arrays:
```
Subject.Teams = [
  {Name: "hr"},
  {Name: "admin"}
]

"teams.name": {"$eq": "hr"}
→ true (weil "hr" in Teams[].Name existiert)
```

---

## 9. Policy Struktur – Beispiele

### 9.1 Beispiel 1: Rollenbasierte Restrict

```json
{
  "policyId": "deny-non-admin-settings",
  "description": "Deny access to settings for non-admins",
  "rules": [
    {
      "effect": "deny",
      "actions": ["oxs:ox.page:read", "oxs:ox.page:write"],
      "subject": {
        "roles": { "$ncontains": "admin" }
      },
      "resource": {
        "section": { "$eq": "settings" }
      }
    }
  ]
}
```

### 9.2 Beispiel 2: Team-basierter Zugriff

```json
{
  "policyId": "hr-team-policy",
  "description": "HR team can access HR documents",
  "rules": [
    {
      "effect": "allow",
      "actions": ["oxs:document:read"],
      "subject": {
        "teams.name": { "$eq": "hr" }
      },
      "resource": {
        "department": { "$eq": "hr" },
        "status": { "$ne": "archived" }
      }
    }
  ]
}
```

### 9.3 Beispiel 3: Komplexe Kombination (Deny + Allow)

```json
{
  "policyId": "document-access-complex",
  "description": "Complex document policy with deny exceptions",
  "rules": [
    {
      "effect": "deny",
      "actions": ["oxs:document:*"],
      "subject": {
        "roles": { "$ncontains": "owner" }
      },
      "resource": {
        "tags": { "$contains": "confidential" }
      }
    },
    {
      "effect": "deny",
      "actions": ["oxs:document:write"],
      "subject": {
        "roles": { "$eq": "viewer" }
      }
    },
    {
      "effect": "allow",
      "actions": ["oxs:document:read", "oxs:document:write"],
      "subject": {
        "$or": [
          {"roles": { "$contains": "owner" }},
          {"teams.name": { "$in": ["admin", "editor"] }}
        ]
      },
      "resource": {
        "status": { "$ne": "deleted" }
      }
    }
  ]
}
```

---

## 10. Validation – Policy Qualitätssicherung

### 10.1 Validierungs-Regeln

Jede Policy wird validiert auf:

- ✓ **Struktur**: PolicyId, Rules vorhanden
- ✓ **Rule-Format**: Effect (allow/deny), Actions, Subject/Resource
- ✓ **Operatoren**: Nur unterstützte Operatoren erlaubt
- ✓ **Typen**: Korrekte Datentypen für Operatoren
- ✓ **Logik**: Gültige AND/OR Kombinationen

### 10.2 Fehlerbehandlung

```csharp
var errors = PolicyEngine.ValidatePolicy(policy);
if (errors.Any())
{
    foreach (var error in errors)
    {
        logger.LogError($"Policy validation error: {error}");
    }
    throw new PolicyValidationException(errors);
}
```

---

## 11. Action Naming Convention

### 11.1 Namensschema

```
Format: service:resource:operation

Komponenten:
  service    = Microservice/Domain (z.B. "oxs", "document", "page")
  resource   = Ressourcentyp (z.B. "document", "page", "user")
  operation  = CRUD-Operation (read, write, delete, execute)
```

### 11.2 Wildcard-Support (Prototype-Semantik: Prefix-Match)

**Logik:** Wenn eine Rule mit `*` endet, wird alles davor als Prefix gematcht (`StartsWith`).

```
Exact Match:
  "oxs:document:read"
  → Matches nur "oxs:document:read"

Suffix-Wildcard (Prefix-Match):
  "oxs:document:*"
  → Matches "oxs:document:read", "oxs:document:write", etc.
  → Matches AUCH "oxs:document:read:special"

Service-Prefix:
  "oxs:*"
  → Matches alle Actions mit Prefix "oxs:"
```

**Wichtig:**
- Nur `*` am Ende hat Sonderbedeutung.
- `*` in der Mitte wird als normaler Text behandelt.

```
Action: oxs:document:read:special

oxs:document:* → Matches (Prefix "oxs:document:")
oxs:*          → Matches (Prefix "oxs:")
oxs:*:*        → Does NOT match (mittleres * ist kein Operator)
oxs:*:read     → Does NOT match (Wildcard nicht am Ende)
```

### 11.3 Beispiele

```
✓ oxs:document:read
✓ oxs:document:write
✓ oxs:document:delete
✓ oxs:document:*
✓ oxs:*            (alle OXS Actions)

✓ oxs:page:read
✓ oxs:page:write

❌ oxs:document   (unvollständig)
❌ oxs:*read      (Wildcard nur am Ende)
❌ oxs:*:*        (mittleres * ohne Sonderbedeutung)
❌ oxs:*:read     (Wildcard nicht am Ende)
```

---

## 12. Redis Caching Architecture – Compiled Policies

### 12.1 Compilation & Caching Model

Policies werden **einmalig kompiliert** und dann **nur in kompilierter Form** gecacht:

```
1. Policy Update Event
   ↓
2. Policy Compiler
   - Parse JSON
   - Validate
   - Compile JSON to typed AST
   - Pre-build MongoDB/Search Filters
   ↓
3. Cache Compiled Policy (only!)
   - Raw Conditions JSON (kanonisch)
   - Pre-built filters
   - No serialized expressions
   ↓
4. Runtime Evaluation
   - Load compiled policy from cache
   - Build matcher from cached AST/JSON in-process
   - No validation overhead
```

### 12.2 Cache-Schichten & Cache-Keys (Organization-Scoped)

```
┌─────────────────────────────────────────────────────────┐
│   L1: Decision Cache (5 min)                            │
│   Key: abac:decision:{orgId}:{userId}:{action}          │
│   Value: true|false                                     │
├─────────────────────────────────────────────────────────┤
│ L2: Compiled Policy Cache (60 min)                      │
│   Key: abac:policy:{orgId}:{policyId}:compiled:{engineSchemaVersion} │
│   Value: CompiledPolicy (JSON serializable)             │
│   - SubjectConditions (parsed at eval)                  │
│   - ResourceConditions (parsed at eval)                 │
│   - MongoDBFilter (pre-built BsonDocument)              │
│   - SearchFilter (pre-built String)                     │
├─────────────────────────────────────────────────────────┤
│ Active Set Pointer                                      │
│   Key: abac:policy:{orgId}:active-set                   │
│   Value: {setId} (z.B. 2026-06-02T08:00Z)               │
├─────────────────────────────────────────────────────────┤
│     Redis (Organization-scoped namespacing)             │
└─────────────────────────────────────────────────────────┘
```

**Entscheidung (v1):**
- L1 Decision Cache bleibt bewusst bei `{orgId}:{userId}:{action}` (kein zusätzlicher Resource-Key).
- Resource-sensitive Entscheidungen werden nicht als L1 Decision gecacht.

**Isolation:**
- Policies einer Org sind **nicht** für andere Orgs sichtbar
- Cache-Keys enthalten **immer** `{orgId}`
- Cross-Org Access ist **nicht möglich** (by design)

### 12.3 Cache Invalidation (Organization-Scoped)

**Bei Policy-Änderung (in Org X)**
```
1. Neue Compiled Policy schreiben/überschreiben
   Key: abac:policy:{orgX}:{policyId}:compiled:{engineSchemaVersion}

2. Decision Caches für alle User in OrgX invalidieren
   Pattern: abac:decision:{orgX}:*
   (nur diese Org betroffen)

3. Alte Compiled-Version bleibt als Fallback bis Grace-Period endet
```

**Bei User-Attributen Änderung (in Org X)**
```
1. Decision Cache für diesen User löschen
   Key: abac:decision:{orgX}:{userId}:*
   (nur dieser User in dieser Org)

2. Compiled Policies bleiben gültig
   (andere User können sie noch nutzen)
```

**Bei Org-Löschung**
```
1. Alle Policies der Org löschen
   Pattern: abac:policy:{orgX}:*

2. Alle Decision Caches der Org löschen
   Pattern: abac:decision:{orgX}:*
```

### 12.4 Compile Contract (wie der Compiler ohne Runtime-Business-Context korrekt baut)

Der zentrale Compiler arbeitet gegen einen deklarativen Contract:

1. **Action Catalog**  
   `action -> erlaubte Subject-/Resource-Felder`
2. **Field Mapping**  
   `Policy-Feld -> Mongo/Search-Zielpfad`
3. **Capability Matrix**  
   `Operator -> Backend-Unterstützung` (Mongo/Search/SignalR)

Nur wenn alle 3 Teile für eine Action vorhanden sind, wird kompiliert.

**Ownership-Modell:**
- Der Contract wird zentral im ABAC-Compiler/Policy-Repo versioniert.
- Fach-Services liefern Änderungen über PRs (neue Actions/Felder/Mapping), aber der zentrale Compiler bleibt die einzige Quelle für das effektive Compile-Verhalten.

### 12.5 Versions-Rollout für Reader-Kompatibilität

1. Compile-Writer schreibt neuen **Set** vollständig (neue Version + setId).  
2. Erst danach wird `abac:policy:{orgId}:active-set` atomar auf den neuen Set umgeschaltet.  
3. Reader verwenden immer den aktiven Set und akzeptieren nur bekannte `engineSchemaVersion`.  
4. Alte Sets bleiben während Grace-Period erhalten (Fallback).  
5. Cleanup der alten Sets erst nach stabilem Rollout.

**Fehlerfall (Version-Mismatch ohne kompatiblen Fallback):**
- Zugriff wird **DENY** entschieden.
- Fehler wird mit hoher Priorität geloggt/alarmiert (`engineSchemaVersion`, `orgId`, `action`, `readerVersion`).

**Grace-Period (Startwert):**
- Alte Sets bleiben standardmäßig **7 Tage** erhalten.

---

## 13. Implementierungs-Checkliste

### Phase 1: Foundation
- [ ] Policy Engine Core (Validierung, Evaluation)
- [ ] Domain Models (Subject, Policy, Rule)
- [ ] ABAC DSL Parser/Evaluator
- [ ] Unit Tests

### Phase 2: Caching
- [ ] Redis Connection Management
- [ ] L1 Decision Cache
- [ ] L2 Policy Cache
- [ ] Cache Invalidation
- [ ] Integration Tests

### Phase 3: Integration
- [ ] MongoDB Filter Compiler
- [ ] Service Middleware
- [ ] Subject Extraction (Auth)
- [ ] E2E Tests

### Phase 4: Operations
- [ ] Monitoring & Metrics
- [ ] Audit Logging
- [ ] Performance Tuning
- [ ] Documentation

---

## 14. Performance-Überlegungen

### 14.1 Latenz-Breakdown

```
Request 1: Cold Cache
  ├─ Load Policies from Redis (miss) → DB: ~50ms
  ├─ Policy Engine Evaluation: 5-10ms
  ├─ Filter Building: 2-3ms
  ├─ MongoDB Query: 50-200ms
  └─ Total: ~150ms

Request 2 (within 5 min): Decision Cache Hit
  ├─ Redis GET (hit): 1-2ms
  ├─ Decision Result: Immediate
  ├─ MongoDB Query: 50-200ms
  └─ Total: ~75ms (50% faster!)

Request 3 (within 30 min): Policy Cache Hit
  ├─ Redis GET (policy list): 1-2ms
  ├─ Policy Engine Evaluation: 5-10ms
  ├─ Filter Building: 2-3ms
  ├─ MongoDB Query: 50-200ms
  └─ Total: ~100ms
```

### 14.2 Redis Memory Usage

```
Per 1000 Policies:
  - Policy List Cache: ~10 KB
  - Compiled Policies: ~5 MB (compressed)
  - Active User Decisions (5000 users): ~25 MB (5-min retention)
  
Total: ~30 MB per 1000 policies
```

### 14.3 Datenbank-Optimierung

```csharp
// Index MongoDB für häufige Resource-Bedingungen
db.documents.createIndex({ "department": 1, "status": 1 });
db.documents.createIndex({ "tags": 1 });
db.documents.createIndex({ "owner": 1 });
```

---

## 15. Testing-Strategie

### 15.1 Unit Tests

```csharp
[Test]
public void WhenDenyAndAllowMatch_ShouldReturnDeny()
{
    var subject = CreateTestSubject();
    var policies = CreateTestPolicies(hasDeny: true, hasAllow: true);
    
    var result = PolicyEngine.GetMatchingRules(subject, policies, "oxs:doc:read");
    
    Assert.That(result, Has.Some.Matches<Rule>(r => r.Effect == "deny"));
}
```

### 15.2 Integration Tests

```csharp
[Test]
public async Task CompleteFlow_ShouldFilterDocumentsCorrectly()
{
    var policy = LoadPolicyFromJson(policyJson);
    var filter = PolicyEngine.BuildMongoFilterFromRules<Document>(rules);
    
    var docs = await collection.Find(filter).ToListAsync();
    
    Assert.That(docs, Is.Not.Empty);
    Assert.That(docs, All.Matches<Document>(d => d.Department == "HR"));
}
```

---

## 16. Monitoring & Observability

### 16.1 Metriken

```
- Policy Evaluation Time (ms)
- Cache Hit Rate (%)
- Allow/Deny Decision Rate
- Validation Errors
- MongoDB Query Time nach Filter
- Redis Connection Health
```

### 16.2 Logging

```csharp
logger.LogInformation(
    "ABAC Decision: User={UserId}, Action={Action}, " +
    "DenyRules={DenyCount}, AllowRules={AllowCount}, Result={Result}",
    userId, action, denyRules.Count, allowRules.Count, result
);
```

### 16.3 Alerts

- ❌ Policy Validation Failures
- ❌ Redis Connection Errors
- ⚠️ Cache Hit Rate < 50%
- ⚠️ Policy Evaluation > 100ms
- ⚠️ MongoDB Query > 500ms

---

## 17. Semantik: Subject & Resource Evaluation

**Status:** ✅ **Entschieden: Option B (Implicit AND)**

### 17.1 Semantik-Definition

**Rule Evaluation Logik:**
```
Eine Rule matched, wenn:
(Subject-Bedingungen erfüllt) AND (Resource-Bedingungen erfüllt)

Mathematik:
  For each Rule: Filter = (Subject-Conditions AND Resource-Conditions)
  Final Result: ( OR(Allow-Filters) ) AND NOT( OR(Deny-Filters) )
```

**Beispiel:**
```json
{
  "effect": "allow",
  "actions": ["oxs:document:read"],
  "subject": { "teams.name": { "$eq": "hr" } },
  "resource": { "department": { "$eq": "hr" }, "status": { "$ne": "archived" } }
}
```

Evaluierung:
```
User "john" in Team "hr" versucht Document mit dept="hr", status="active" zu lesen
→ Subject-Match: "hr" in teams? YES
→ Resource-Match: dept="hr" AND status!="archived"? YES
→ Result: ALLOW ✅

Wenn Document status="archived" hat:
→ Resource-Match: dept="hr" AND status!="archived"? NO
→ Result: DENY (implizit) ❌
```

### 17.2 Leere Bedingungen

**Definition:**
- `"subject": {}` oder `null` = **"Alle User"** (true für jeden Subject)
- `"resource": {}` oder `null` = **"Alle Ressourcen dieser Action"** (true für jede Resource)

**Beispiel:**
```json
{
  "effect": "allow",
  "actions": ["oxs:document:read"],
  "subject": {},              // Keine Subject-Bedingung
  "resource": { "public": { "$eq": true } }
}
```

Bedeutung: **"Jeder User darf öffentliche Dokumente lesen"**

```json
{
  "effect": "deny",
  "actions": ["oxs:document:delete"],
  "subject": { "roles": { "$eq": "admin" } },
  "resource": {}              // Keine Resource-Bedingung
}
```

Bedeutung: **"Admins dürfen KEINE Dokumente löschen"** (überall)

### 17.3 Cross-Attribute References

**Status:** ✅ **Unterstützt (mit Variablenersetzung)**

**Definition:**
Resource-Bedingungen können auf Subject-Werte referenzieren via `{subject.attributName}`:

```json
{
  "effect": "allow",
  "actions": ["oxs:document:read"],
  "subject": { "department": { "$eq": "sales" } },
  "resource": { "owner_department": { "$eq": "{subject.department}" } }
}
```

Evaluierung für User mit `department="sales"`:
```
1. Subject-Match: department="sales"? YES
2. Subject-Wert extrahieren: {subject.department} → "sales"
3. Resource-Match: owner_department = "sales"? (depends on doc)
4. Result: ALLOW wenn beide true
```

**Technische Implementierung (einfach):**
```csharp
// 1. Subject evaluieren & Werte extrahieren
var subjectValues = EvaluateSubject(subject, rule.SubjectConditions);
// → { "department": "sales" }

// 2. Variablen in Resource-Filter ersetzen
var resourceWithVariables = ReplaceVariables(
    rule.ResourceConditions,
    subjectValues  // {subject.department} → "sales"
);

// 3. Resource-Bedingung evaluieren/filtern
var resourceMatch = EvaluateResource(resource, resourceWithVariables);
```

**Syntax:**
- `{subject.fieldName}` - direktes Feld
- `{subject.nested.field}` - verschachtelte Felder
- `{subject.arrays[0].name}` - Array-Zugriff (nur Index 0)

**Nicht unterstützt:**
- `{resource.xyz}` (nur Subject-Referenzen)
- Funktionen/Transformationen (z.B. `{subject.email.toLowerCase()}`)

---

## 18. Zusammenfassung

### Kernmerkmale

| Merkmal | Wert |
|---------|------|
| **Ansatz** | Policy-basiert, Deny > Allow |
| **Standard** | Implizites Deny |
| **Fallback** | Allow-All möglich |
| **Bedingungen** | Standard Condition Operators |
| **Subject & Resource** | Implicit AND (beide müssen matchen) |
| **Variablenersetzung** | `{subject.xxx}` in Resource-Bedingungen |
| **Integration** | Middleware zwischen Service & DB |
| **Filter-Aufbau** | Dynamisch pro Request |
| **Caching** | Multi-Level Redis Cache (Organization-scoped) |

### Vorteil

✅ **Feinkörnig**: Attribute-basierte Policies  
✅ **Sicher**: Deny-First, Zero-Trust-Modell  
✅ **Flexibel**: Komplexe Bedingungen kombinierbar  
✅ **Wartbar**: Zentrale Policy-Verwaltung  
✅ **Hochperformant**: Redis Multi-Level Caching  
✅ **Isoliert**: Organization-scoped  

---

**Ende des Konzepts**
