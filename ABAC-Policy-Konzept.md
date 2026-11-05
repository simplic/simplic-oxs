# ABAC Policy Engine – Ausgearbeitetes Konzept

**Version:** 1.1  
**Datum:** Oktober 2026  
**Status:** In Umsetzung

---

## 1. Überblick

Ein **Attribute-Based Access Control (ABAC)** System mit Policy-Engine für feinkörnige Zugriffskontrolle zwischen Service und Datenbank. Das System basiert auf einem **Deny-First-Ansatz** mit expliziten Allow-Rules und implizitem Deny als Standardverhalten.

### Kernprinzipien
- **Policy-orientiert**: Zentrale Verwaltung von Zugriffsregeln
- **Deny > Allow**: Deny-Policies haben höhere Priorität
- **Implizites Deny**: Standardmäßig Zugriff verweigert (Zero-Trust)
- **Allow-All-Fallback**: Optional Basis-Policy für vollständigen Zugriff
- **Redis-Caching**: Multi-Level Cache für Policies und Decisions
- **Service-lokale Persistenz**: Jeder Service hält die für ihn relevanten Compiled Rules
  durable in seiner eigenen DB (Redis = heißer Cache, §12.6)
- **Identity = Quelle der Wahrheit**: Policies **und** Feld-Mappings werden zentral in
  Identity verwaltet und an die Services synchronisiert (§12.6, §12.7)
- **Durchsetzung im Data-Layer**: Jeder Egress-Kanal erbt die Filter (§7.6)

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
           │  Event (Push) + Pull-API (§12.6)
    ┌──────▼──────────────────┐
    │ Service-lokaler Rule-   │
    │ Store (Service-DB)      │
    │ - relevante Compiled    │
    │   Rules (durable)       │
    │ - Sync/Max-Age 1h       │
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

#### Service-lokaler Rule-Store + Sync (§12.6)
- Shared-Library-Komponente (`Simplic.OxS.Data.MongoDB/Abac`), von jedem Service nur registriert
- Persistiert die für den Service relevanten Compiled Rules in dessen eigener MongoDB
  (Collection `abac.compiled-rules`)
- Bus-Consumer (Push-Trigger) + Sync-Job (Pull bei Bootstrap, Max-Age, Reconciliation)
- Durable Quelle für den Runtime-Reader, wenn Redis kalt/nicht verfügbar ist

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

#### Service-lokal gespeicherte Compiled Rules (§12.6)

Collection `abac.compiled-rules` in der DB **jedes** Fach-Services (Shared-Library-Komponente).
Gespeichert werden die **vollständigen** Compiled Rules der eigenen Actions — die Filter allein
reichen nicht, da der Evaluator auch Subject-Conditions/Effect/Action-Patterns benötigt.

```csharp
public class StoredCompiledRuleSet
{
    public Guid OrganizationId { get; set; }        // immer org-scoped
    public string SetId { get; set; }               // Set aus Identity (§12.5)
    public string EngineSchemaVersion { get; set; }
    public bool IsActive { get; set; }              // aktiver vs. vorheriger Set
    public bool IsConfirmedEmpty { get; set; }      // Identity bestätigt: Org hat KEINE Policies (§6.4)
    public DateTime SyncedAt { get; set; }          // Basis für Max-Age (1h) / Hard-Limit (24h)
    public IList<CompiledRule> Rules { get; set; } = []; // nur Rules mit Actions dieses Services
}
```

Indizes: `{ OrganizationId: 1, IsActive: 1 }` (unique für aktive Sets), `{ OrganizationId: 1, SetId: 1 }`.

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
  (*Ausnahme während des Rollouts:* temporärer Allow-All-Fallback bei **von Identity
  bestätigtem** leerem Policy-Set, siehe §6.4)
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

### 6.4 Rollout: Bestehende & neue Organisationen

**Neue Organisationen:** Bei Anlage wird die Allow-All-Basis-Policy (§6.2) automatisch
geseedet. Die Organisation schränkt sie danach über Deny-/spezifische Allow-Rules ein.

**Bestehende Organisationen (Erst-Rollout):** Statt jede Org vorab zu seeden, gilt ein
**temporärer impliziter Allow-All-Fallback**, wenn für eine Organisation **keine Policy**
existiert. Damit bleibt das Verhalten beim Einschalten von ABAC unverändert.

**Sicherheitsregeln (verbindlich):**
- Der Fallback greift **nur**, wenn Identity für die Org ein **bestätigt leeres** Policy-Set
  ausgeliefert hat (expliziter Marker `IsConfirmedEmpty` im synchronisierten Set, §3/§12.6).
- **Fehlende, nicht ladbare oder über das Hard-Limit (24h) veraltete Daten** führen weiterhin
  zu **DENY** — ein Redis-/Sync-Ausfall darf niemals stillschweigend Vollzugriff gewähren.
- Sobald die Org mindestens eine Policy besitzt, gilt normales Zero-Trust (§5).
- Der Fallback ist **übergangsweise**: gesteuert über eine globale bzw. per-Org-Einstellung
  und wird nach Abschluss des Rollouts entfernt (dann wieder striktes Zero-Trust).

**Steuerung der Einstellung** *(entschieden 2026-10-26)*:
- **Globaler Default** per App-Konfiguration in Identity: `Abac:Rollout:AllowAllWhenEmpty`
  (`true` während des Rollouts, danach `false`).
- **Per-Org-Override** als Organisations-Setting über die bestehende Bibliothek
  `Simplic.OxS.Settings` (`IOrganizationSettingsProvider`) in Identity.
- **Schreiben nur mit Rolle `owner`** (sicherheitsrelevanter Fail-Open-Schalter, strenger als §10.3).
- Identity liefert den effektiven Wert **mit dem Set** aus (Flag neben `IsConfirmedEmpty`); Services
  lesen die Einstellung nicht selbst, damit Entscheidung und Daten konsistent bleiben.

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
  │       (durable Quelle je Service = eigene Service-DB, §12.6; Redis = heißer Cache)
  │
  ├─► Action Mapping
  │   └─► HTTP-Request → ABAC-Action
  │       Beispiel: GET /documents → oxs:document:read
  │
  └─► Policy Engine
      │
      ├─► Regelauswertung (Subject/Action)
      │
      ├─► Wenn Deny (Action) → 403 Forbidden  (Details §7.5)
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

**Verbindlich — beide Modi kombinieren:** Der parameterlose `Authorize(org, action)`-Aufruf
(ohne Resource) wertet **keine Resource-Conditions** aus: bedingte Deny-Regeln (Deny mit
Resource-Bedingung) werden hier bewusst ignoriert. Er beantwortet nur, ob der Subject die Action
**grundsätzlich** – auf *irgendeiner* Ressource – ausführen darf. Als **alleinige** Absicherung eines
datenliefernden Pfads ist er damit **unzureichend** und kann Datensätze durchlassen, die eine
bedingte Deny-Regel verbergen würde. Er **muss** daher immer mit der Datenebenen-Prüfung kombiniert
werden – der Resource-Überladung `Authorize(org, action, resource)` für Einzelobjekte bzw.
`BuildListFilterAsync` / `BuildSearchFilterAsync` für Read-Many. Standalone ist der parameterlose
Aufruf nur für grobes Endpoint-Reachability-Gating zulässig. (Der org-scoped Repository-Basistyp
wendet `BuildListFilterAsync` über `IAbacReadFilterProvider` nur an, wenn das abgeleitete Repository
`AbacReadAction` überschreibt **und** ein `IAbacReadFilterProvider` verfügbar ist. Standardmäßig ist
`AbacReadAction` `null` – ohne dieses Opt-in wird **kein** ABAC-Filter angewendet, §7.6.)

### 7.5 Response-Semantik bei Deny (verbindlich)

| Operation | Situation | Response |
|-----------|-----------|----------|
| **List / Search** (Read-Many) | Action für den Subject komplett verweigert (keine passende Allow-Rule / Blanket-Deny) | **403 Forbidden** |
| **List / Search** | Action erlaubt, aber keine Daten matchen die Resource-Filter | **200** mit **leerer Liste** |
| **Single Read** | Action verweigert | **403 Forbidden** |
| **Single Read** | Action erlaubt, Ressource existiert, wird aber durch Resource-Conditions herausgefiltert | **404 Not Found** (kein Existenz-Leak) |
| **Commands** (Create/Update/Delete) | Deny | **403 Forbidden** |

Anonyme Aufrufe (kein Subject) auf ABAC-geschützten Endpunkten → Deny (§17.4).

### 7.6 Egress-Kanäle – Durchsetzung im Data-Layer

ABAC wird **im gemeinsamen Data-Layer** (Repository / Mongo-Filter-Erweiterung) durchgesetzt,
sodass **jeder** Kanal, der über das Repository liest, die Filter automatisch erbt.
Kanal-spezifische Durchsetzung nur dort, wo kein Repository genutzt wird.

Abgedeckte Kanäle:
- **REST-Controller** (List/Read/Commands)
- **GraphQL (HotChocolate)** — Queries und Subscriptions
- **gRPC-Endpunkte**
- **Exports / Reports / Datei-Downloads**
- **Internal Client** (Service-zu-Service-Antworten; gleiches Subject wie externe Aufrufer, §17.4)
- **Suche (Elasticsearch)** — eigener Pfad über `BuildSearchFilterAsync` (§18)
- **SignalR** — Broadcast-Filter (offen, §13.1)

**Message-Bus-Events (vorläufige Regel):** Events mit Entitätsdaten werden nicht pro
Empfänger gefiltert. Stattdessen gilt: Hat eine Organisation **irgendeine relevante
Deny-Regel** für die betroffenen Daten definiert, ist das **Joinen/Konsumieren dieser Daten
über den Bus für diese Organisation verboten**. Einfachste sichere Lösung für den Start;
feingranulare Event-Filterung ist spätere Arbeit.

**Konkretisierung** *(entschieden 2026-10-26)*:

*Data-Layer:* Durchsetzung im Repository-Basistyp von `Simplic.OxS.Data.MongoDB`
(org-scoped Repository): Lesezugriffe (`Get`, `GetByFilter`, `GetAll`, Paging, `Count`) ANDen den
ABAC-Mongo-Filter der jeweiligen `read`-Action **nur im Opt-in**: Das abgeleitete Repository muss
`AbacReadAction` überschreiben (Default `null`) und es muss ein `IAbacReadFilterProvider`
verfügbar sein. Fehlt eines davon, wird **kein** ABAC-Filter angewendet. Kanäle in der Shared Library:
- **GraphQL:** `GraphQLExtension.UseGraphQL` (HotChocolate + Mongo-Filtering/Paging) — Resolver
  müssen ihre Daten über das Repository bzw. einen ABAC-gefilterten `IExecutable` beziehen.
- **gRPC:** `GrpcSecurityInterceptor`/`GrpcAuthenticationMiddleware` setzen Subject/Org-Kontext
  wie die HTTP-Pipeline; die Daten kommen über das Repository.
- **Internal Client:** leitet Org-/User-Kontext und Service-Namen weiter (§17.4).
- **Exports/Reports:** liegen in den einzelnen Fach-Services; sie müssen das Repository nutzen.
  Jeder Service erstellt beim ABAC-Rollout ein **Egress-Inventar** (Liste aller Endpunkte mit
  Datenausgabe + Nachweis der Repository-Nutzung).

*Message-Bus:*
- **Publisher** bleiben unverändert (keine Filterung beim Senden).
- **Consumer, die ungeschützte Kopien bilden** (Read-Models, Denormalisierung, Joins fremder
  Domänendaten): prüfen pro Nachricht, ob die Org eine Deny-Rule mit Action-Pattern für die
  betroffene `read`-Action besitzt (lokaler Rule-Store, §12.6). Wenn ja → Nachricht **acken und
  überspringen** (nicht verwerfen für andere Consumer), Log + Metrik `abac_bus_skipped`.
- **Ausgenommen** sind Consumer, deren Ziel ABAC selbst durchsetzt — insbesondere die
  **Such-Indexierung** (Durchsetzung zur Query-Zeit über `security.*`, §18).

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

### 10.3 Policy-Verwaltung: Berechtigung & Audit

**Berechtigung der Admin-Endpunkte** (Policies **und** Feld-Mappings, §12.7) — beide Bedingungen
müssen erfüllt sein:
1. **Rollen-Gate (außerhalb von ABAC):** Nur User mit Rolle **`owner`** oder **`admin`** dürfen die
   Endpunkte überhaupt aufrufen. Das verhindert, dass sich eine Organisation über eine
   fehlerhafte Policy selbst aussperrt.
2. **ABAC:** Zusätzlich über eigene Actions geschützt:
   `oxs:abac-policy:read`, `oxs:abac-policy:write`, `oxs:abac-policy:activate`
   (analog für Mappings `oxs:abac-mapping:*`).

**Rollen-Quelle** *(entschieden 2026-10-26)*: JWT-Claim `Roles`
(JSON-Array, wie von `AbacSubjectFactory` gelesen) enthält `owner` oder `admin`
(Vergleich case-insensitive). Ausnahme: der Rollout-Schalter (§6.4) nur `owner`.

**Audit:** Jede Änderung wird in einer **unveränderlichen Änderungshistorie** in Identity
abgelegt: wer, wann, Vorher/Nachher-Diff, und welcher `setId` aktiviert wurde.

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

### 11.4 Such-Actions (verbindlich)

Für die Suche gibt es **kein eigenes Verb**. Suchen über einen Datentyp erfordert die
**Lese-Action** dieses Typs:

```
Suche mit dataType = X   →  oxs:{X}:read
```

**Typübergreifende Suche** (kein `dataType` angegeben): Der Filter wird **pro Datentyp**
gebildet und per OR kombiniert — in **einer** ES-Query:

```
OR über alle Datentypen X, für die der Subject oxs:{X}:read besitzt:
    ( dataType = X  AND  ABAC-Filter(oxs:{X}:read) )
```

Datentypen ohne Leserecht sind damit ausgeschlossen. Hat der Subject für **keinen** Datentyp
Leserecht → **403** (§7.5).

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

3. Service-lokale Rule-Stores (§12.6): Org-Löschung wird per Event propagiert;
   jeder Service löscht alle `abac.compiled-rules`-Einträge der Org
```

**Service-lokale Rule-Stores bei Policy-Änderung (§12.6)**
```
1. Identity publiziert Event "Set aktiviert" (orgId, setId, engineSchemaVersion)
2. Jeder Service pullt den neuen Set, filtert lokal auf seine Actions,
   schreibt ihn vollständig und schaltet IsActive atomar um
3. Ältere Sets als der vorherige werden gelöscht (Retention: aktiv + vorheriger)
4. Betroffene Decision-Caches der Org invalidieren
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
- **Feld-Mappings** sind **per Organisation** in Identity verwaltet (Quelle der Wahrheit, §12.7);
  die statische Konfig `Abac:CompileContract:FieldMappings` ist nur ein Übergang.

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

### 12.6 Service-lokale Persistenz der relevanten Compiled Filter (verbindlich)

**Anforderung:** Jeder Fach-Service **muss** die für ihn relevanten ABAC-Filter
(die vorkompilierten Mongo-/Search-Filter der Actions, die er bedient) **in seiner
eigenen Service-Datenbank** persistieren. Redis bleibt der **heiße Cache**, ist aber
**nicht** die Quelle der Wahrheit für den einzelnen Service — ein flüchtiger Cache
(Eviction, Cold Start, Redis-Ausfall) darf die Autorisierung nicht unmöglich machen.

**Begründung:**
- **Fail-Closed-Resilienz.** Ohne durable Kopie würde ein leerer/ausgefallener Redis
  bei Fail-Closed-Semantik (§5, §18) **alles** auf DENY setzen → faktischer Ausfall
  des Services. Die Service-DB garantiert, dass der Runtime-Reader auch bei kaltem
  Cache einen gültigen aktiven Set findet.
- **Service-Autonomie & Entkopplung.** Der Query-Pfad eines Services hängt zur Laufzeit
  nicht mehr synchron an Identity/zentralem Store; jeder Service besitzt die Filter,
  die er zur Durchsetzung braucht.
- **Nur relevanter Ausschnitt.** Ein Service speichert **ausschließlich** die Filter
  seiner eigenen Actions/Ressourcen — nicht den gesamten Policy-Bestand der Organisation.

**Modell (Hybrid: Push-Trigger + Pull):**
```
Identity (Compiler, Quelle der Wahrheit)
   │  kompiliert aktiven Set (Version + setId, §12.5)
   │
   ├─► PUSH: Event "AbacPolicySetActivated" (orgId, setId, engineSchemaVersion)
   │         über MassTransit/RabbitMQ (Simplic.OxS.MessageBroker) — nur Trigger
   │
   └─◄ PULL-API: liefert den vollständigen aktiven Set der Org
                  (inkl. Marker IsConfirmedEmpty, wenn die Org keine Policies hat)
   ▼
Service X (Shared-Library-Komponente)
   ├─► Bus-Consumer: Event empfangen → Pull auslösen
   ├─► Sync-Job: Pull bei Bootstrap/neuer Org, bei Max-Age-Überschreitung,
   │             periodische Reconciliation
   ├─► filtert den Org-Set LOKAL auf die eigenen Action-Prefixes
   ├─► persistiert die relevanten vollständigen Compiled Rules in `abac.compiled-rules`
   ├─► optional in Redis gespiegelt (heißer Cache)
   └─► Runtime-Reader: Redis → Service-DB → DENY
```

**Entscheidungen:**
- **Propagation: Hybrid.** Das Event ist nur ein Trigger; die Daten kommen immer über die
  Pull-API. Bootstrap (neuer Service, neue Org, leere DB) und Reconciliation laufen ebenfalls
  über Pull. Verlorene/doppelte Events sind dadurch unkritisch (Pull ist idempotent).
- **Relevanz: lokal gefiltert.** Identity liefert den **vollständigen Org-Set**; jeder Service
  filtert lokal auf die Rules, deren Action-Patterns seine eigenen Actions betreffen. Identity
  benötigt dafür **kein** Service→Action-Register.
- **Gespeicherter Inhalt: vollständige Compiled Rules** der eigenen Actions (Action-Patterns,
  Effect, Subject-Conditions, vorkompilierte Mongo-/Search-Filter, Metadata) — Filter allein
  reichen für die Evaluierung nicht. Datenmodell: §3.
- **Aktualität (Max-Age):** Ist der letzte erfolgreiche Sync (`SyncedAt`) älter als **1h**
  (konfigurierbar), wird **neu gepullt**.
- **Stale-Fallback:** Schlägt der Re-Pull fehl (Identity nicht erreichbar), arbeitet der Service
  mit den veralteten Daten weiter (Warn-Log + Alert), **bis zum Hard-Limit von 24h**
  (konfigurierbar). Danach → **DENY** (Fail-Closed).
- **Implementierung: Shared Library** (`Simplic.OxS.Data.MongoDB/Abac`): Collection
  `abac.compiled-rules`, Bus-Consumer und Sync-Job; jeder Service registriert sie nur.
- **Retention:** Nur **aktiver + vorheriger** Set (schnelles Rollback); ältere Sets werden nach
  erfolgreicher Aktivierung gelöscht.
- **Schreibschutz:** Ausschließlich die Sync-Komponente schreibt; **keine** API und kein
  Repository bietet Schreibzugriff auf die Collection.

**Regeln:**
- **Set-/Versions-Semantik** wie §12.5: ein neuer Set wird vollständig geschrieben, dann
  `IsActive` atomar umgeschaltet.
- **Reader-Reihenfolge:** Redis (heiß) → Service-DB (durable) → DENY, wenn kein gültiger
  (≤ Hard-Limit) aktiver Set vorliegt. Ein Treffer in der DB **befüllt** den Redis-Cache neu.
- **Leere Org:** Nur ein Set mit `IsConfirmedEmpty = true` darf den temporären
  Allow-All-Fallback auslösen (§6.4); fehlende Daten nie.
- **Invalidation/Update:** Bei neuem Set Decision-Caches der Org invalidieren (§12.3).
- **Isolation:** Einträge sind **immer** `{orgId}`-scoped (§12.2).
- **Engine-Version:** Nur bekannte `engineSchemaVersion`; Mismatch ohne Fallback → DENY (§12.5).

> Status: **offen** — derzeit liest der Runtime-Reader (`AbacPolicyReader`) ausschließlich
> aus Redis. Store, Consumer, Sync-Job und Pull-API sind noch nicht implementiert (§13.1).

### 12.7 Feld-Mappings: Identity als Quelle der Wahrheit (verbindlich)

Feld-Mappings verbinden **Policy-Feld ↔ Security-Key/-Typ ↔ JSON-Pfad (pro `dataType`)**.
Sie werden **per Organisation in Identity** verwaltet — Identity ist die einzige Quelle der
Wahrheit, damit Compiler (Query-Seite) und Such-Materialisierung (Index-Seite) nie auseinanderlaufen.

- Der **Compiler** liest die Org-Mappings direkt (ersetzt die statische, globale Konfig
  `Abac:CompileContract:FieldMappings`).
- Der **Such-Service** synchronisiert die Org-Mappings auf dem **gleichen Hybrid-Weg** wie
  Policies (Event + Pull, Max-Age, §12.6) und materialisiert damit `security.*` (§18.11).
- Eine Mapping-Änderung löst in Identity einen Recompile **und** im Such-Service den Reindex
  der Org aus.
- Verwaltung nur über die Admin-API (Rollen-Gate owner/admin + ABAC, Audit, §10.3).

> **Migration:** Die bereits implementierte, search-lokale `SecurityAttributeConfig`-CRUD
> (§18.11 Schritt 4) wird zur **synchronisierten Kopie** (ohne eigenen Schreib-Endpunkt);
> CRUD und Konfig-Modell wandern nach Identity (§13.1).

**Zusammengeführtes Modell** *(entschieden 2026-10-26)*:

Heute existieren zwei Modelle: global `AbacFieldMapping` (Identity-Konfig: Feld, `MongoDbPath`,
`SearchPath`, `SearchValuePrefix`) und per Org `SecurityAttributeConfig` (Search:
`securityKey`, `jsonPath`, `type`, optional je `DataType`). Ziel ist **ein** per-Org-Modell in Identity:

```
AbacFieldMappingSet (Collection abac.field-mappings, je Org ein Dokument, versioniert)
├── OrganizationId
├── Version
└── Mappings[]
    ├── Field          Policy-Feldname (z.B. "department")
    ├── Type           principal | classification | attribute
    ├── SecurityKey    Schlüssel im ES-security-Block (Default = Field)
    ├── MongoDbPath    Pfad im Fach-Dokument für den Mongo-Filter
    └── JsonPaths      { dataType → jsonPath }  ("" = Default für alle Typen)
```

- `SearchPath`/`SearchValuePrefix` werden **abgeleitet**, nicht gepflegt:
  `attribute` → `security.attributes.{SecurityKey}`; `principal` → `security.principals` mit Präfix
  `{SecurityKey}:`; `classification` → `security.classification`.
- Der Such-Service leitet seine `SecurityAttributeConfig` (je `DataType`: `securityKey`, `jsonPath`,
  `type`) deterministisch aus diesem Modell ab — Compiler und Materialisierung nutzen dieselbe Quelle.
- **Migration:** einmaliger Job importiert die bestehenden `SecurityAttributeConfig` jeder Org aus
  dem Such-Service (interner Endpunkt) nach Identity. Bis zur Migration einer Org bleibt die statische
  Konfig `Abac:CompileContract:FieldMappings` als globaler Fallback aktiv; danach entfällt sie.

---

## 13. Implementierungs-Checkliste

> **Stand:** 2026-10-26. Status basiert auf einer Code-Prüfung über beide Repos
> (`simplic-oxs` = Read-/Runtime-Seite, `simplic-oxs-identity` = Write-/Compiler-Seite)
> sowie dem Such-Microservice `simplic-oxs-search`.
> Legende: ✅ fertig · 🟡 teilweise · ❌ offen.

### Phase 1: Foundation
- [x] ✅ Policy Engine Core (Evaluation) — `AbacEvaluator` (deny-first), `simplic-oxs`
- [x] ✅ Domain Models (Subject, Policy, Rule) — `Simplic.OxS.ABAC/*`
- [x] ✅ ABAC DSL Parser/Evaluator — `ConditionNodeParser`, `AbacConditionEvaluator`, `AbacResourceConditionEvaluator`
- [x] ✅ Unit Tests — oxs 184, Identity 126, Search 60 (Stand 2026-10-26, alle grün)
- [x] ✅ Policy-**Validierung** — `AbacPolicyValidator` (Identity) gegen `IAbacCompileContract`; 400 mit Fehlerliste, `POST AbacPolicy/validate`

### Phase 2: Caching
- [x] ✅ Redis Connection Management — `RedisExtension`, `IDistributedCache`
- [x] ✅ L1 Decision Cache — `IMemoryCache` im `AbacEvaluator` (Key enthält SetId; `Abac:L1:Ttl`, Default 30s)
- [x] ✅ L2 Policy Cache — Schreiben: Identity-Compile + Service-Sync; Lesen: `AbacPolicyReader`
- [x] ✅ Cache Invalidation — Auto-Recompile bei CRUD (`Abac:AutoCompile`); neue SetId invalidiert L1 implizit
- [x] ✅ Service-lokaler Rule-Store (§12.6) — `abac.compiled-rules`, Consumer, Sync-Job, Pull-API, Fallback Redis → DB → DENY
- [~] 🟡 Integration Tests — Testcontainers-Projekte vorhanden (oxs 4, Identity 3, Search 11); **noch nie gegen Docker gelaufen**

### Phase 3: Integration
- [x] ✅ MongoDB Filter Compiler — `AbacMongoFilterBuilder` mit `MongoDbPath`, fail-closed
- [x] ✅ Service Middleware — CRUD, Compile/Activate, Auto-Recompile, Admin-Gate, Audit
- [x] ✅ Subject Extraction (Auth) — `AbacSubjectFactory` aus JWT-Claims
- [ ] ❌ E2E Tests (service-übergreifend inkl. RabbitMQ)
- [x] ✅ Resource-Condition-Evaluation im `Authorize`-Pfad (Overload mit Resource)
- [x] ✅ Search-Filter-Pfad inkl. Mapping-Sync aus Identity (Details §18.10)
- [x] ✅ SignalR-Broadcast-Filter — Helper `IAbacSignalRFilter.CanReceiveAsync` (Hub-Integration je Service)

### Phase 4: Operations
- [x] ✅ Monitoring & Metrics — Meter `Simplic.OxS.Abac` (§16)
- [x] ✅ Audit Logging — unveränderliche Collection `abac.audit` in Identity
- [ ] ❌ Performance Tuning (erst nach Lasttests)
- [x] ✅ Documentation (dieses Konzept)

---

## 13.1 Offene Arbeiten (priorisiert)

> **Stand 2026-10-26 (Implementierung):** Alle P1–P3-Punkte sind in `simplic-oxs`,
> `simplic-oxs-identity` und `simplic-oxs-search` implementiert (uncommittet). Build aller drei
> Solutions grün; Unit-Tests 184 / 126 / 60 grün; Testcontainers-Tests (4 / 3 / 11) werden ohne
> Docker übersprungen. Verbleibend sind nur noch die unten mit `[ ]` markierten Punkte.

### P0 — Voraussetzung: Release der ABAC-Runtime
*(entschieden 2026-10-26)*
- [ ] **ABAC-Runtime committen und als NuGet veröffentlichen.** `Simplic.OxS.ABAC`,
  `Simplic.OxS.Server/Abac` und `Simplic.OxS.Data.MongoDB/Abac` sind noch uncommittete lokale Arbeit.
  Release über `azure-pipelines.yml` (NuGet); danach stellen `simplic-oxs-search` **und**
  `simplic-oxs-identity` (Identity referenziert `Simplic.OxS.ABAC` per ProjectReference) auf
  **PackageReference** um, Identity zusätzlich auf eine `Simplic.OxS.Server`-Version mit Runtime.
  Die ProjectReferences (Geschwister-Pfad) dürfen **nicht** gemergt werden.
- [ ] **Testcontainers-Tests einmal mit Docker ausführen** (CI-Agent mit Docker oder lokal).

### Entscheidungen zu offenen Punkten (Stand 2026-10-26)

| # | Thema | Entscheidung | Status | Abschnitt |
|---|-------|--------------|--------|-----------|
| 1 | Aufrufer-Identität | Authentifizierung unverändert (JWT/API-Key); intern = extern, kein Service-Subject | ✅ bestätigt | §17.4 |
| 2 | Admin-Rollen | JWT-Claim `Roles` enthält `owner`/`admin` | ✅ bestätigt | §10.3 |
| 3 | Feld-Mapping-Modell | Ein per-Org-`AbacFieldMappingSet` in Identity, Search-Konfig abgeleitet, Import-Migration | ✅ bestätigt | §12.7 |
| 4 | Rollout-Schalter | Global `Abac:Rollout:AllowAllWhenEmpty` + Org-Override, nur `owner` | ✅ bestätigt | §6.4 |
| 5 | Egress-Kanäle | Durchsetzung im `Simplic.OxS.Data.MongoDB`-Repository; Egress-Inventar je Service | ✅ bestätigt | §7.6 |
| 6 | Message-Bus | Konsumierende Kopien: acken + überspringen; Such-Indexierung ausgenommen | ✅ bestätigt | §7.6 |
| 7 | Release | NuGet-Release vor weiterer Integration; ProjectReference nicht mergen | ✅ bestätigt | §13.1 P0 |

### Umsetzung — Referenz

**Shared Contracts (`Simplic.OxS.ABAC`)**
- `Sync.AbacPolicySetActivated`, `Sync.AbacFieldMappingsChanged` (Events = nur Trigger),
  `Sync.AbacOrganizationRuleSet` (Pull-DTO), `Sync.AbacSyncEndpoints`.
- `Mapping.AbacFieldMappingSet` / `AbacFieldDefinition` / `AbacFieldType` (abgeleiteter `SearchPath`,
  `SearchValuePrefix` = `SecurityKey` ohne `:` – der Builder ergänzt den Separator).
- `Runtime.AbacPolicySnapshot` (Status `Unavailable`/`Loaded`/`ConfirmedEmpty`, Quelle, `IsStale`),
  `IAbacPolicyReader.GetSnapshotAsync`, `IAbacEvaluator.AuthorizeAsync(…, resource)`,
  `HasDenyRulesForActionAsync`, `AbacSubjectVariables`, `AbacResourceConditionEvaluator`.
- `AbacAdminActions`, `AbacRolloutSettings`, `AbacMetrics`; Cache-Keys `Flags(org,setId)`
  (`abac:policy:{org}:flags:{setId}` → `{"isConfirmedEmpty","allowAllWhenEmpty"}`), `FieldMappings(org)`.

**Runtime (`simplic-oxs`)**
- Rule-Store `MongoAbacRuleStore` (`abac.compiled-rules`, Retention aktiv + vorheriger),
  `IAbacRuleSyncService`/`IAbacRuleSetClient` (Pull `GET /identity-api/v1/internal/AbacPolicySync/rule-set`,
  `i-api-key` + `OrganizationId`-Header), `AbacPolicySetActivatedConsumer` (**nicht** auto-registriert –
  Service leitet mit `[Consumer]` im eigenen Assembly ab), `AbacRuleSyncBackgroundService`.
- Konfig: `Abac:Sync:{Enabled, Interval=5m, MaxAge=1h, HardLimit=24h, PullTimeout=3s, FailureBackoff=30s,
  RedisTtl=60m, ActionPrefixes, Service, BaseUrl, Scheme, ApiVersion}`, `Abac:L1:{Enabled, Ttl=30s}`.
- `AbacEvaluator`: Snapshot-basiert, Rollout-Fallback, Authorize (Action-Ebene) getrennt von
  `BuildListFilterAsync` (Allow → `$or`, Deny mit Resource → `$nor`, nicht übersetzbar → fail-closed),
  Resource-Overload, `{subject.xxx}`-Substitution (`$eq` mit Mehrfachwert → `$in`/`terms`), L1-Cache, Metriken.
- Egress: `MongoOrganizationRepositoryBase` opt-in über `AbacReadAction` + `IAbacReadFilterProvider`
  (auch `GetCollection()` für GraphQL); Deny bei Listen → `AbacAccessDeniedException` →
  `AbacAccessDeniedExceptionFilter` (403); gefilterter Single-Read → `null` → 404.
- `AbacBusGuard.ShouldSkipAsync` (§7.6), `IAbacSignalRFilter.CanReceiveAsync`.
- Registrierung: `AddAbacRuntime()` / `AddAbacRuntime(IConfiguration)`.

**Identity (`simplic-oxs-identity`)**
- `AbacPolicyValidator`, `AbacMongoFilterBuilder`, PolicyId-Eindeutigkeit (409), Auto-Recompile,
  Confirmed-Empty-Set statt Fehler, `abac.compiled-sets` (Quelle der Pull-API), Event-Publish.
- Admin-Gate (`AbacAdminAuthorizer` + `AbacAdminAuthorizeAttribute`): Rolle owner/admin **und**
  ABAC-Action; Bootstrap: bei leerem Set genügt die Rolle. Rollout-PUT nur `owner`.
- Endpunkte: `AbacPolicy` (CRUD, `validate`, `compile`, `audit`, `rollout`), `AbacFieldMapping`
  (`GET`, `PUT`, `POST import`), `internal/AbacPolicySync` (`rule-set`, `field-mappings`; Internal-Key).
- Collections: `abac.compiled-sets`, `abac.audit` (append-only), `abac.field-mappings`, `abac.rollout-settings`.

**Search (`simplic-oxs-search`)**
- `AbacFieldMappingSyncService` (Versions-Skip, Version 0 = nicht migriert → kein Überschreiben,
  Reindex nur bei Änderung), `AbacFieldMappingsChangedConsumer`, `SearchAbacPolicySetActivatedConsumer`.
- `SecurityAttributeConfig` ist synchronisierte Kopie: nur `GET` + `POST sync` (owner/admin);
  `Abac:Sync:ActionPrefixes = ["oxs:"]`.

### Verbleibende Punkte
- [ ] **Seeding Allow-All für neue Orgs verdrahten** — `IAbacPolicyService.SeedDefaultPolicyAsync(orgId)`
  existiert; Identity hat keinen Organization-Created-Hook → Consumer ergänzen, sobald das Event existiert.
- [ ] **Rollout-Override in `Simplic.OxS.Settings` statt `abac.rollout-settings`** — bewusst abweichend
  umgesetzt, da der generische Settings-Controller Owner-Gate und Recompile umgehen würde. Entscheidung bestätigen.
- [ ] **Egress-Inventar je Fach-Service** — Repositories mit `AbacReadAction` versehen, Consumer
  mit `AbacBusGuard`, Hubs mit `IAbacSignalRFilter`, `IAbacCompileContract`-Katalog je Service.
- [ ] **Periodischer Mapping-Resync in Search** (heute nur Event + `POST sync`); Reindex ohne verteiltes Lock.
- [ ] **Bereichsoperatoren mit mehrwertigen `{subject.xxx}`-Variablen** (nur `$eq` → `$in` umgesetzt).
- [ ] **Migration bestehender Search-`SecurityAttributeConfig` nach Identity** (Import nutzt bisher nur die statische Identity-Konfig).
- [ ] **E2E-Tests** über RabbitMQ (Activate → Event → Service-Sync) und Performance-Tuning.

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

Request 4: Redis kalt/ausgefallen → Service-DB-Fallback (§12.6)
  ├─ Redis GET (miss/timeout): 1-5ms
  ├─ MongoDB `abac.compiled-rules` (Index orgId+IsActive): 2-10ms
  ├─ Redis-Rehydrierung (async, nicht im kritischen Pfad)
  ├─ Policy Engine Evaluation + Filter Building: 7-13ms
  ├─ MongoDB Query: 50-200ms
  └─ Total: ~110ms
```

**Sync-Pfad (nicht im Request-Pfad):** Pull eines Org-Sets aus Identity erfolgt asynchron
(Event, Max-Age-Ablauf, Reconciliation). Der Request wartet nie auf Identity — ist kein gültiger
Set (≤ Hard-Limit 24h) lokal vorhanden, gilt DENY.

### 14.2 Redis Memory Usage

```
Per 1000 Policies:
  - Policy List Cache: ~10 KB
  - Compiled Policies: ~5 MB (compressed)
  - Active User Decisions (5000 users): ~25 MB (5-min retention)
  
Total: ~30 MB per 1000 policies
```

**Service-DB (`abac.compiled-rules`, §12.6):** pro Org max. 2 Sets (aktiv + vorheriger), nur die
Rules der eigenen Actions → typischerweise wenige KB–MB je Org und Service; vernachlässigbar.

### 14.3 Datenbank-Optimierung

```csharp
// Index MongoDB für häufige Resource-Bedingungen
db.documents.createIndex({ "department": 1, "status": 1 });
db.documents.createIndex({ "tags": 1 });
db.documents.createIndex({ "owner": 1 });
```

---

## 15. Testing-Strategie

**Framework:** xUnit (`[Fact]`/`[Theory]`, immer `async Task`, nie `async void`).
**Infrastruktur:** Integrationstests laufen gegen echte Abhängigkeiten via **Testcontainers**
(Redis, MongoDB, RabbitMQ, Elasticsearch) — keine Mocks für Store/Cache/Bus/Index.

### 15.1 Unit Tests

```csharp
[Fact]
public void WhenDenyAndAllowMatch_ShouldReturnDeny()
{
    var subject = CreateTestSubject();
    var policies = CreateTestPolicies(hasDeny: true, hasAllow: true);

    var result = PolicyEngine.GetMatchingRules(subject, policies, "oxs:doc:read");

    Assert.Contains(result, r => r.Effect == "deny");
}
```

### 15.2 Integration Tests (Testcontainers)

```csharp
public class AbacMongoFilterTests : IClassFixture<MongoContainerFixture>
{
    [Fact]
    public async Task CompleteFlow_ShouldFilterDocumentsCorrectly()
    {
        var policy = LoadPolicyFromJson(policyJson);
        var filter = PolicyEngine.BuildMongoFilterFromRules<Document>(rules);

        var docs = await collection.Find(filter).ToListAsync();

        Assert.NotEmpty(docs);
        Assert.All(docs, d => Assert.Equal("HR", d.Department));
    }
}
```

**Pflicht-Szenarien:**
- **Mongo:** Allow/Deny-Kombinationen, Org-Isolation, Seeding der 3 Key-Tiers.
- **Sync (§12.6):** Event → Pull → Store; Redis kalt → DB-Fallback; Max-Age-Re-Pull;
  Identity down → Stale-Nutzung bis Hard-Limit → DENY; `IsConfirmedEmpty` vs. fehlende Daten.
- **Elasticsearch:** materialisierte `security.*`-Felder + ABAC-`filter` liefern exakt die erlaubten
  Treffer; Deny-First; Pagination/Hit-Counts korrekt; typübergreifendes OR (§11.4).
- **Response-Semantik (§7.5):** 403 / 404 / 200-leer je Fall.

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
- Sync-Lag je Org/Service (jetzt − SyncedAt)
- Sync-Fehler (Pull/Consumer) je Org/Service
- Service-DB-Fallback-Rate (Redis miss → DB hit)
- Stale-Nutzung (Requests mit Set älter als Max-Age)
- Hard-Limit-DENYs (Set älter als 24h)
- Allow-All-Rollout-Fallback-Nutzung (§6.4)
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
- ❌ Hard-Limit erreicht (Org/Service fällt auf DENY)
- ❌ Engine-Schema-Version-Mismatch ohne Fallback
- ⚠️ Stale-Daten in Nutzung (Re-Pull fehlgeschlagen, Max-Age überschritten)
- ⚠️ Sync-Fehlerrate erhöht / Consumer-Lag auf RabbitMQ

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

### 17.4 Subjects: interne/externe Aufrufer & Anonym

**Entschieden (2026-10-26):** Es gibt **keinen eigenen Service-Account-Subject-Typ**. Die
Authentifizierung bleibt unverändert (JWT bzw. API-Key/`InternalApiKey`). Für den Server ist es
**unerheblich, ob ein Aufruf intern oder extern ist** — auch externe Aufrufer authentifizieren sich
über denselben Key-Mechanismus.

- Das Subject wird **immer gleich** aus dem authentifizierten Kontext gebaut (`AbacSubjectFactory`:
  Claims `Id`, `OId`, `Roles`, `Teams`). Der `ApiKeyAuthenticationHandler` setzt `Id`/`OId` bereits;
  interne Aufrufe übernehmen Org-/User-Kontext wie bisher über `RequestContextActionFilter`.
- Gleiche Engine, gleiche Deny-First-Semantik, **kein** Bypass für interne Aufrufe.
- Fehlt nach der Authentifizierung ein gültiger Org-/User-Kontext → kein Subject → **DENY**.

**Anonyme Aufrufe:**
- Anonym → **immer DENY** auf allen Actions, die Daten ausgeben.
- `[AllowAnonymous]` ist auf Endpunkten mit Datenausgabe **verboten** (im `SearchController`
  bereits entfernt, §13.1).

---

## 18. Such-Integration (Elasticsearch) – ABAC Enforcement

**Status:** 🟡 Übersetzungs-Schicht + ES-`security`-Mapping + Index-Zeit-Materialisierung
(Konfig je Organisation + CRUD + Reindex-Trigger, §18.11) + **Feld-Pfad-Mapping**
(`AbacFieldMapping.SearchPath`/`SearchValuePrefix`, fail-closed, in den Compiler verdrahtet)
+ **Such-Consumer** (`SearchController` → `BuildSearchFilterAsync` → ES-`filter`-Klausel;
`simplic-oxs-search` auf net10 gehoben; Action `oxs:{X}:read`, Deny → 403, typübergreifendes OR,
`[AllowAnonymous]` entfernt) implementiert & (Unit-)getestet. Offen: ES-Integrationstest und
Feld-Mapping-Sync aus Identity (§12.7). Detaillierter Done/Offen-Stand
in §18.10, priorisierte Restliste in §13.1.

Für MongoDB ist das Resource-Filtering unkompliziert, weil die Policy-Felder direkt
im Dokument liegen. In Elasticsearch ist die Situation schwieriger: Ein Suchdokument
besteht typischerweise aus `searchText` (Volltext), `dataType` (keyword) und `data`
(die fachlichen Daten als serialisierter JSON-String). **ABAC darf niemals gegen den
`data`-String zur Query-Zeit ausgewertet werden** — das wäre bei Millionen Dokumenten
nicht performant und verhindert korrekte Pagination/Aggregation/Hit-Counts.

### 18.1 Grundprinzip: Autorisierungs-Attribute zur Index-Zeit materialisieren

Autorisierungs-relevante Attribute werden **beim Indexieren** aus den Fachdaten
abgeleitet und als **indexierte, getypte Felder** gespeichert — getrennt von den
Fachdaten. Der `data`-String bleibt nur zur Rückgabe erhalten (`_source`, nicht
indexiert), wenn `searchText` bereits alles Suchrelevante enthält.

```
Dokument
├── searchText        text        (Volltextsuche)
├── dataType          keyword
├── data              _source only, index:false   (nur Retrieval)
└── security          ← zur Index-Zeit abgeleitet, NICHT zur Query-Zeit
    ├── tenantId      keyword      (= OrganizationId, defensiv; Isolation über Index-Namen)
    ├── principals    keyword[]    (z.B. "department:finance")
    ├── classification integer     (für range-Vergleiche)
    └── attributes    flattened    (seltene/dynamische Attribute)
```

> **Hinweis (Deployment `simplic-oxs-search`):** Kein `owner`-Feld — die Daten sind
> organisationsverwaltet, es gibt keine Datensatz-Ownership. Die Tenant-Isolation erfolgt
> bereits über den organisationsspezifischen Index-Namen; `tenantId` ist nur defensiv.

### 18.2 Hybrid-Mapping gegen Mapping-Explosion

Nicht jedes mögliche ABAC-Attribut als eigenes Mapping-Feld anlegen — dynamisches
JSON kann zu tausenden Feldnamen führen (Default-Limit:
`index.mapping.total_fields.limit = 1000`). Stattdessen:

- **Häufig gefilterte, stark getypte** ABAC-Dimensionen → **dedizierte Felder**
  (`security.tenantId`, `security.classification`, `security.principals`).
- **Seltene/dynamische** Attribute → ein einziges **`flattened`-Feld**
  (`security.attributes`), das den gesamten Objektbaum als ein Mapping-Feld abbildet.

**Wichtige Einschränkung:** `flattened` behandelt alle Werte als Keywords — kein
Numerik-/Range-/Datums-Verhalten. ABAC-Dimensionen, die `$gt/$gte/$lt/$lte` benötigen
(z.B. `classification`), **müssen** dedizierte getypte Felder sein, niemals `flattened`.

### 18.3 ABAC-DSL → Elasticsearch Query DSL (Operator-Mapping)

Wie bei MongoDB bleibt die **Subject-Evaluation in-process (C#)**: Zur Query-Zeit ist
bekannt, welche Regeln für den aktuellen Subject greifen. Nur die **Resource-Conditions**
der passenden Regeln werden in ES-Filter übersetzt (analog zu `BuildListFilterAsync`).

| DSL-Operator | ES-Filter | Hinweis |
|--------------|-----------|---------|
| `$eq`  | `term`                 | exakt (keyword/numeric) |
| `$ne`  | `bool.must_not` + `term`  | |
| `$in`  | `terms`                | Posting-List-Intersektion (schnell) |
| `$nin` | `bool.must_not` + `terms` | |
| `$gt` `$gte` `$lt` `$lte` | `range` | nur getypte Felder, **nicht** `flattened` |
| `$contains` `$ncontains` | ❌ nicht übersetzbar | per `SupportsSearch=false` sperren (s.u.) |

Das Feld-Mapping (`department` → `security.attributes.department` bzw.
`security.classification`) kommt aus `AbacFieldMapping.SearchPath` des Compile-Contracts
(§12.4). Cross-Attribute-Variablen (`{subject.xxx}`, §17.3) werden beim Compilieren der
Query aus den Subject-Werten eingesetzt und sind damit in ES unkompliziert.

### 18.4 Deny-First in Elasticsearch

Die Deny-First-Semantik (§4, §17.1) muss der ES-Compiler **explizit** abbilden:
`finalFilter = OR(Allow-Resource-Filter) AND NOT(OR(Deny-Resource-Filter))`.

```json
{
  "query": {
    "bool": {
      "must": [
        { "match": { "searchText": "ACME contract" } }
      ],
      "filter": [
        { "term": { "security.tenantId": "{orgId}" } },

        { "bool": {
            "should": [
              { "terms": { "security.attributes.department": ["finance", "legal"] } },
              { "term":  { "security.owner": "user-123" } }
            ],
            "minimum_should_match": 1
        }},

        { "bool": {
            "must_not": [
              { "term": { "security.attributes.status": "archived" } }
            ]
        }}
      ]
    }
  }
}
```

Regeln für den Aufbau:
- `security.tenantId` (OrganizationId) ist **immer** als `term`-Filter gesetzt —
  Cross-Org-Zugriff ist strukturell unmöglich.
- `should`-Gruppe = OR aller Allow-Resource-Filter; `minimum_should_match: 1`.
- Hat eine passende Allow-Regel **keine** Resource-Condition → unbeschränkter Allow
  für diese Action → die `should`-Gruppe entfällt (Analog zu `allowAllResources`).
- Matched **keine** Allow-Regel → Match-Nothing-Filter (`match_none`) = implizites Deny.
- `must_not`-Gruppe = OR aller Deny-Resource-Filter.

### 18.5 Operator-Gating & Fallback

Nicht jeder DSL-Operator ist in ES sinnvoll (z.B. `$contains` = Substring auf keyword
→ nur via teurem `wildcard`). Das wird über
`AbacOperatorCapabilities.SupportsSearch` gesteuert:

- `SupportsSearch = true` → Operator wird in einen ES-Filter übersetzt.
- `SupportsSearch = false` → Regel ist **nicht** vollständig in ES erzwingbar. Optionen:
  1. Policy-Validierung lehnt die Regel für search-Actions ab (bevorzugt), **oder**
  2. definierter In-Process-Nachfilter auf der (kleinen) Treffermenge — nur zulässig,
     wenn dadurch keine unautorisierten Treffer in Counts/Aggregationen gelangen.

Der sichere Default ist Option 1: lieber bei Compile-Zeit ablehnen als zur Laufzeit
unsicher nachfiltern.

### 18.6 Index-Zeit-Ableitung bewusst minimal halten (Reindex-Kosten)

Der zentrale Trade-off gegenüber MongoDB:
- **MongoDB** wertet zur Query-Zeit gegen Live-Felder aus → Policy-Änderungen wirken sofort.
- **Elasticsearch** filtert gegen index-zeitlich abgeleitete `security.*`-Felder.

Daher gilt:
- **Rohe Attribut-Projektion** (`department`, `classification`, `owner`) ist
  policy-agnostisch — ändert sich nur das Dokument, wird nur dieses Dokument reindexiert. ✅
- **Materialisierte `principals`/Autorisierungs-Dimensionen** koppeln den Index an
  Policy-Logik. Ändert sich die Policy, kann ein **Voll-Reindex über Millionen
  Dokumente** nötig werden. Diese Optimierung nur einsetzen, wenn das Policy-Set stabil
  ist; die Reindex-Kosten explizit einplanen.

Empfehlung: **rohe, policy-agnostische** Security-Attribute indexieren und die
**Policy in die Query** compilieren. So bleibt das Mapping stabil, auch wenn neue
`dataType`s hinzukommen, und Policy-Änderungen erfordern keinen Reindex.

### 18.7 Keine native ES-DLS / keine gefilterten Aliase als Sicherheitsgrenze

- **Native Document-Level-Security** von Elasticsearch wird **nicht** genutzt: Die
  ABAC-Engine ist die einzige Quelle der Wahrheit (ein Autorisierungsmodell, pro Backend
  ein Compiler — Mongo **und** ES). Keine doppelte, unabhängige Autorisierung im Store.
- **Gefilterte Aliase** sind laut Elastic **kein** sicherer Mechanismus zur
  Zugriffsbeschränkung und werden nicht als Sicherheitsgrenze verwendet.

### 18.8 Einordnung in den Compile-Contract

Diese Such-Integration füllt die bereits vorgesehenen, aber noch leeren Bausteine:
- `AbacFieldMapping.SearchPath` — Policy-Feld → ES-Zielpfad.
- `AbacOperatorCapabilities.SupportsSearch` — Operator-Gating (§18.5).
- `CompiledRule.SearchFilter` — der vorcompilierte ES-Filter (als JSON-String), analog
  zu `MongoDbFilterJson`.
- Laufzeit-seitig fehlen noch ein `BuildSearchFilterAsync` auf `IAbacEvaluator` sowie
  eine Apply-Extension analog zu `ApplyAbacFilter` (siehe §13.1 P2).

### 18.9 Request-Pfad

```
User-Attribute (JWT)
      │
      ▼
 ABAC-Engine ──► Mongo-Prädikat ──────► MongoDB
      │
      └────────► ES-Filter (bool/term/terms/range + must_not)
                        │
                 Suchquery + Security-Filter
                        │
                        ▼
                 Elasticsearch ──► nur autorisierte Treffer
```

Elasticsearch gibt so **nie** unautorisierte Dokumente an die Anwendung zurück;
Pagination, `total hits` und Aggregationen bleiben korrekt.

**Ziel-Semantik (verbindlich, §7.5/§11.4):**
- Action ist `oxs:{dataType}:read` (keine eigene `:search`-Action).
- Typübergreifende Suche: **ein** ES-Query mit `bool.should` über alle für den Subject lesbaren
  Typen, je Typ `dataType = X AND filter(oxs:X:read)`; Typen mit Action-Deny entfallen.
- Kein lesbarer Typ bzw. Action-Deny → **403**; erlaubt ohne Treffer → 200 mit leerem Ergebnis.
- Anonym → DENY/401 (`[AllowAnonymous]` entfällt, §17.4).

### 18.10 Implementierungs-Stand (Done / Offen)

Die **Übersetzungs-Schicht** (ABAC-DSL → ES Query DSL, geteilte Bibliothek + Compiler)
ist implementiert und mit Unit-Tests abgesichert. Das **Elasticsearch-Mapping** (getyptes
`security`-Objekt + `flattened`) ist im Such-Microservice `simplic-oxs-search` implementiert.
Die **Index-Zeit-Materialisierung** der `security.*`-Felder (§18.11) und das **Feld-Pfad-Mapping**
im Übersetzer sind ebenfalls erledigt. Auch der **Such-Consumer** (Query-Seite) ist verdrahtet:
`SearchController` evaluiert pro Request über `IAbacEvaluator.BuildSearchFilterAsync` und hängt
den Filter in die ES-Query ein — gemäß Ziel-Semantik §18.9 (`oxs:{X}:read`, 403, typübergreifendes
OR, kein `[AllowAnonymous]`). Offen sind der **ES-Integrationstest** und der Mapping-Sync aus
Identity (§12.7).

**✅ Erledigt (geteilte Bibliothek + Compiler, getestet):**
- [x] **AST → ES Query DSL Übersetzer** — `AbacSearchFilterBuilder` (§18.3): `$eq`→term,
  `$ne`→must_not+term, `$in`→terms, `$nin`→must_not+terms, `$gt/$gte/$lt/$lte`→range,
  `$and`→bool.filter, `$or`→bool.should (`minimum_should_match:1`). → `Simplic.OxS.ABAC/Search/AbacSearchFilterBuilder.cs`
- [x] **Operator-Gating** — `$contains`/`$ncontains` werfen `AbacUnsupportedSearchOperatorException`;
  Compiler fängt ab → `SearchFilter = null` (§18.5). → `Simplic.OxS.ABAC/Search/AbacUnsupportedSearchOperatorException.cs`
- [x] **Deny-First-Komposition** — `EvaluateSearchAsync`: Allow-Rules → `should`-Gruppe,
  Deny-Rules → `must_not`; Fail-Closed, wenn Resource-Conditions nicht ausdrückbar (§18.4).
  → `Simplic.OxS.Server/Abac/AbacEvaluator.cs`
- [x] **Contract-Erweiterung** — `IAbacEvaluator.BuildSearchFilterAsync` +
  `AbacDecisionResult.SearchFilterJson`. → `Simplic.OxS.ABAC/Runtime/`
- [x] **Query-Combiner** — `AbacSearchQuery.Apply(userQueryJson, abacFilterJson)`
  (User-Query in `must`, ABAC in `filter`). → `Simplic.OxS.ABAC/Search/AbacSearchQuery.cs`
- [x] **Compiler füllt `CompiledRule.SearchFilter`** analog zu `MongoDbFilterJson` (§18.8).
  → `simplic-oxs-identity/.../AbacPolicyService.cs`
- [x] **Unit-Tests** — Builder (eq/ne/in/nin/range/and/or/unsupported) +
  11 Evaluator-Such-Pfad-Szenarien inkl. Fail-Closed. → `Simplic.OxS.Server.Test/Abac/`
- [x] **Feld-Pfad-Mapping** — `AbacFieldMapping.SearchPath` (z.B. `department` →
  `security.attributes.department`) + `SearchValuePrefix` (Principal-Felder →
  `security.principals` mit `key:value`-Präfix, spiegelt die Materialisierung) werden im
  Übersetzer angewendet. **Fail-closed:** unbekanntes Feld, fehlender `SearchPath` oder
  Range-Operator auf einem Principal-Feld werfen; ohne Mappings (Standalone) bleibt der
  Rohfeld-Passthrough. Verdrahtet über den Compile-Contract (`IAbacCompileContract.FieldMappings`,
  konkrete `ConfiguredAbacCompileContract` + `AbacCompileContractOptions`, DI in der Identity).
  → `Simplic.OxS.ABAC/Search/AbacSearchFilterBuilder.cs`,
  `Simplic.OxS.ABAC/Contract/AbacFieldMapping.cs`,
  `simplic-oxs-identity/.../ConfiguredAbacCompileContract.cs`, `AbacCompileContractOptions.cs`,
  `AbacPolicyService.cs`

**✅ Erledigt (Such-Microservice `simplic-oxs-search`):**
- [x] **ES-Index-Mapping / `security`-Schema** — `DocumentSecurity` (getyptes `tenantId`
  keyword, `principals` keyword[], `classification` integer, `attributes` **flattened**);
  `IndexDocument.Security` ergänzt; zentraler Mapping-Helper `ElasticSearchProvider.MapIndexDocument`
  an allen drei Index-Erzeugungsstellen (Live-Index + beide Reindex-Pfade). **Ohne `owner`**
  (organisationsverwaltete Daten, keine Datensatz-Ownership). Bestehende Indizes erhalten das
  Mapping erst beim Reindex. → `Simplic.OxS.Search/Model/DocumentSecurity.cs`,
  `Simplic.OxS.Search.Service/ElasticSearchProvider.cs`
- [x] **Index-Zeit-Materialisierung** (§18.1, §18.11) — `security.*`-Felder werden in
  `DefaultFactory.Create` aus den Fachdaten (`DataJson`) abgeleitet. Konfig-Store +
  `ISecurityAttributeProvider` (Schritt 1–2), Extraktion via `SecurityAttributeMaterializer`
  (Schritt 3), sowie Konfig-CRUD + Reindex-Trigger mit Re-Materialisierung (Schritt 4) —
  alle unit-getestet. → `Simplic.OxS.Search.Service/DefaultFactory.cs`,
  `SecurityAttributeMaterializer.cs`, `SecurityAttributeConfigService.cs`,
  `Simplic.OxS.Search.Server/Controllers/SecurityAttributeConfigController.cs`

**✅ Erledigt (Such-Consumer, Query-Seite):**
- [x] **Such-Consumer verdrahtet** — `SearchController.Search` ruft
  `IAbacEvaluator.BuildSearchFilterAsync(orgId, action, ct)` auf und übergibt den
  resultierenden `SearchFilterJson` an den `ISearchProvider`. `ElasticSearchProvider.Search`
  hängt ihn als `filter`-Klausel (Raw-Query) neben der User-Query (`must`:
  `searchText` + optionaler `dataType`-Term) in die ES-Query ein (§18.4/§18.9).
  **Deny ⇒ 403**, `Allow` ohne Filter ⇒ unbeschränkt, `Allow` mit Filter ⇒ eingehängt.
  Action: `oxs:{dataType}:read` (§11.4). **Ohne `dataType`** (typübergreifend): Datentypen des
  Org-Index per Terms-Aggregation (`ISearchProvider.GetDataTypes`), je Typ `oxs:{X}:read` evaluiert,
  erlaubte Typen per `SearchAbacFilterComposer.ComposeCrossType` zu einem `bool.should` aus
  `(dataType = X AND filter)` komponiert (nicht gelistete Typen matchen nie — fail-closed);
  kein lesbarer Typ ⇒ 403, leerer Index ⇒ 200 leer. `[AllowAnonymous]` entfernt.
  → `simplic-oxs-search/.../SearchController.cs`, `ElasticSearchProvider.cs`,
  `ISearchProvider.cs`, `SearchAbacFilterComposer.cs` (+ 5 Unit-Tests), `Startup.cs`
  (`services.AddAbacRuntime()`)
- [x] **net10-Angleichung** — `simplic-oxs-search` von net8.0 auf net10.0 gehoben;
  `Simplic.OxS.Server` wird als **ProjectReference** auf das Geschwister-Repo bezogen
  (die ABAC-Runtime ist noch nicht im veröffentlichten NuGet-Paket), zusätzlich
  `Simplic.OxS.ABAC`-ProjectReference. Build + bestehende Tests grün.

**⚠️ Hinweise:**
- Such-Aufrufe ohne gültigen Org-/User-Kontext (weder JWT noch API-Key) erhalten jetzt 401/403;
  key-authentifizierte Aufrufe werden wie alle anderen über ihr Subject ausgewertet (§17.4).
- Typübergreifende Suche evaluiert eine Action je Datentyp im Index (max. 1000 Typen per
  Aggregation); bei sehr vielen Typen ggf. Decision-Cache nötig (§13.1 L1-Cache).
- Feld-Mappings: Identity ist Quelle der Wahrheit; der Such-Service synchronisiert sie (§12.7).
  Organisationen ohne migriertes Mapping-Set (Version 0) behalten ihre lokale Konfiguration.

**⛔ Offen:**
- [~] **Integrationstest gegen echtes Elasticsearch** (Testcontainers, §15) — Projekt
  `Simplic.OxS.Search.IntegrationTest` (11 Tests inkl. typübergreifender Suche) vorhanden;
  wird ohne Docker übersprungen, muss noch einmal mit Docker laufen.
- [x] **Mapping-Sync aus Identity** (§12.7): `SecurityAttributeConfig` ist synchronisierte Kopie
  (`AbacFieldMappingSyncService`, Event + `POST SecurityAttributeConfig/sync`).

### 18.11 Index-Zeit-Materialisierung – Konfiguration je Organisation (Design)

Der Such-Index erhält über `IndexDataCommand` nur `Id`, `DataType` und `DataJson` — **keine
Autorisierungs-Attribute**. Diese müssen daher beim Indexieren aus `DataJson` abgeleitet werden.
Welche JSON-Pfade autorisierungsrelevant sind, ist **pro Organisation (optional pro `DataType`)
unterschiedlich** und nicht statisch bekannt. Deshalb braucht es eine Konfiguration plus einen
Reindex, wenn sie sich ändert.

**Geplante Bausteine:**
1. **Konfigurationsmodell & -speicher** — ✅ *erledigt.* Pro Organisation (optional pro `DataType`)
   eine Liste von `{ securityKey, jsonPath, type: principal | classification | attribute }`.
   Mongo-gestützt, analog zu `SummaryTemplate`. → `Simplic.OxS.Search/Model/SecurityAttributeConfig.cs`,
   `SecurityAttributeMapping.cs`, `SecurityAttributeType.cs`, `SecurityAttributeConfigFilter.cs`;
   Repository `Simplic.OxS.Search.Data.DB/SecurityAttributeConfigRepository.cs` (Collection
   `search.security-attribute-config`).
2. **`ISecurityAttributeProvider`** — ✅ *erledigt.* Lädt die Konfiguration der Organisation und
   merged die Mappings für einen `DataType` (globale Config mit leerem `UsedForDataType` +
   DataType-spezifische). Default = leer ⇒ sicherer No-Op. Unit-getestet (7 Fälle). Provider-seitiges
   Caching + Invalidierung bleibt bewusst offen (koppelt an Cache-Invalidierung); der Reindex nutzt
   stattdessen einen lokalen Mapping-Cache pro `DataType`.
   → `Simplic.OxS.Search.Service/SecurityAttributeProvider.cs`,
   `Simplic.OxS.Search.Test/SecurityAttributeProviderTest.cs`
3. **Extraktion in `DefaultFactory.Create`** — ✅ *erledigt.* `DefaultFactory` bezieht die Mappings
   über `ISecurityAttributeProvider`, löst die `JsonPath`-Werte per Dot-Notation (`SelectToken`)
   aus `DataJson` auf und füllt `security.attributes.*`, `security.classification` bzw.
   `security.principals` (`SecurityAttributeType`); `tenantId` wird defensiv gesetzt. Array-Werte
   ergeben mehrere Principals bzw. eine Liste im Attribut; fehlende/ungültige Pfade und
   Nicht-Objekt-JSON werden übersprungen (fail-open beim Indexieren, Leer-Default sicher).
   Unit-getestet (12 Fälle). → `Simplic.OxS.Search.Service/SecurityAttributeMaterializer.cs`,
   `DefaultFactory.cs`, `Simplic.OxS.Search.Test/SecurityAttributeMaterializerTest.cs`
4. **Reindex-Trigger & Konfig-CRUD** — ✅ *erledigt.* `SecurityAttributeConfigService`
   (CRUD, `OrganizationServiceBase`) + `SecurityAttributeConfigController` (GET/GET-ALL/POST/
   PATCH/DELETE, analog `TemplateController`). Nach Create/Update/Delete wird der bestehende
   Reindex der betroffenen Organisation im Hintergrund angestoßen. Der Reindex selbst
   **re-materialisiert** jetzt den `security`-Block jedes Dokuments aus dessen `Original`-Fachdaten
   (via `ISecurityAttributeProvider` + `SecurityAttributeMaterializer`, Mappings pro `DataType`
   gecacht) — zuvor kopierte er nur Felder und hätte die Konfig-Änderung nicht übernommen.
   → `Simplic.OxS.Search.Service/SecurityAttributeConfigService.cs`,
   `Simplic.OxS.Search.Service/ElasticSearchProvider.cs` (Reindex-Schleife + `MaterializeSecurityAsync`),
   `Simplic.OxS.Search.Server/Controllers/SecurityAttributeConfigController.cs`
5. **Feld-Pfad-Mapping** (§18.10) — ✅ *erledigt.* `AbacFieldMapping.SearchPath` +
   `SearchValuePrefix` werden im `AbacSearchFilterBuilder` angewendet (Attribute →
   `security.attributes.*`, Principals → `security.principals` mit `key:value`-Präfix,
   `classification` → getyptes `security.classification` mit Range-Support). Fail-closed bei
   unbekanntem Feld / fehlendem `SearchPath` / Range auf Principal. Verdrahtet über den
   Compile-Contract (`IAbacCompileContract.FieldMappings` → `ConfiguredAbacCompileContract` +
   `AbacCompileContractOptions`, Konfig-Sektion `Abac:CompileContract`, DI in der Identity).
   **Hinweis:** Die `FieldMappings` der Identity müssen mit den per-Organisation
   `SecurityAttributeConfig`-Pfaden des Such-Repos übereinstimmen.

**Reihenfolge-Abhängigkeit:** Der Such-Consumer (Query-Seite) liefert erst dann korrekte
Ergebnisse, wenn die Dokumente die `security.*`-Felder tragen (Materialisierung) **und** die
Filter auf deren Pfade zeigen (Feld-Pfad-Mapping).

> **Zielbild (§12.7):** Die Konfiguration aus Schritt 1/4 wird nach Identity verlagert (per Org,
> Quelle der Wahrheit). Der Such-Service hält `SecurityAttributeConfig` nur noch als
> **synchronisierte Kopie** (Event + Pull); der eigene CRUD-Endpunkt entfällt, der Reindex wird
> durch den Sync ausgelöst.

---

## 19. Zusammenfassung

### Kernmerkmale

| Merkmal | Wert |
|---------|------|
| **Ansatz** | Policy-basiert, Deny > Allow |
| **Standard** | Implizites Deny |
| **Fallback** | Allow-All nur temporär im Rollout bei bestätigt leerem Set (§6.4) |
| **Bedingungen** | Standard Condition Operators |
| **Subject & Resource** | Implicit AND (beide müssen matchen) |
| **Subjects** | Ein Subject-Modell für alle Aufrufer (JWT/API-Key, intern = extern); anonym immer DENY (§17.4) |
| **Variablenersetzung** | `{subject.xxx}` in Resource-Bedingungen |
| **Integration** | Durchsetzung in der Shared-Data-Layer, alle Egress-Kanäle (§7.6) |
| **Response-Semantik** | Action-Deny → 403, gefilterter Einzelzugriff → 404, kein Treffer → 200 leer (§7.5) |
| **Such-Backend** | Elasticsearch: Security-Attribute zur Index-Zeit, Filter zur Query-Zeit (§18) |
| **Filter-Aufbau** | Dynamisch pro Request |
| **Quelle der Wahrheit** | Identity (Policies + Feld-Mappings, per Org; §12.6/§12.7) |
| **Caching/Persistenz** | Redis (heiß) + service-lokaler Rule-Store in der Service-DB (§12.6) |

### Vorteil

✅ **Feinkörnig**: Attribute-basierte Policies  
✅ **Sicher**: Deny-First, Zero-Trust-Modell  
✅ **Flexibel**: Komplexe Bedingungen kombinierbar  
✅ **Wartbar**: Zentrale Policy-Verwaltung  
✅ **Hochperformant**: Redis Multi-Level Caching  
✅ **Resilient**: Service-lokaler Rule-Store, keine Laufzeit-Abhängigkeit zu Identity  
✅ **Isoliert**: Organization-scoped  

---

**Ende des Konzepts**
