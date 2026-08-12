# Simplic.OxS Exceptions

This directory contains the framework's HTTP-aware exception model. Throwing one of these
exceptions from **any layer** (domain, service, controller) produces a consistent, RFC 9457
`application/problem+json` error response — you never build the response yourself.

## How it works

Every exception here derives from [`OxSException`](OxSException.cs), which carries its own HTTP
metadata but has **no ASP.NET dependency**. A global handler chain in the server layer
(`Simplic.OxS.Server/Exceptions/Handlers/`) reads that metadata and writes the response. It is wired
up centrally in `Bootstrap` — services do **not** register their own exception filters or middleware.

```
throw new OxSException-derived   ─▶   OxSExceptionHandler   ─▶   application/problem+json
   (any layer)                        (reads metadata,            { type, title, status,
                                        logs by severity)           detail, instance, traceId, … }
```

The response body always contains: `type` (a problem-type URN), `title`, `status`, `detail`
(the exception `Detail`, which defaults to `Message`), `instance` (request path) and `traceId`.
Some exceptions add extra members (e.g. `errors`, `resource`) or headers (e.g. `Retry-After`).

Handled exceptions are logged automatically, with severity by status class: **5xx → Error**,
**401/403 → Warning**, **other 4xx → Information**. You do not need to log them at the throw site.

## Available exceptions

| Exception | Status | Problem type URN (`urn:simplic-oxs:problem:…`) | Extras |
|---|---|---|---|
| [`BadRequestException`](BadRequestException.cs) | 400 | `…:bad-request` | optional `errors` (field map) |
| [`UnauthorizedException`](UnauthorizedException.cs) | 401 | `…:unauthorized` | `WWW-Authenticate: Bearer` header |
| [`ForbiddenException`](ForbiddenException.cs) | 403 | `…:forbidden` | — |
| [`NotFoundException`](NotFoundException.cs) | 404 | `…:not-found` | **anonymous** — no resource info |
| [`ResourceNotFoundException`](ResourceNotFoundException.cs) | 404 | `…:not-found` | `resource` / `resourceType` / `resourceId` |
| [`ConflictException`](ConflictException.cs) | 409 | `…:conflict` | — |
| [`PayloadTooLargeException`](PayloadTooLargeException.cs) | 413 | `…:payload-too-large` | — |
| [`UnsupportedMediaTypeException`](UnsupportedMediaTypeException.cs) | 415 | `…:unsupported-media-type` | — |
| [`UnprocessableEntityException`](UnprocessableEntityException.cs) | 422 | `…:unprocessable-entity` | optional `errors` (field map) |
| [`TooManyRequestsException`](TooManyRequestsException.cs) | 429 | `…:too-many-requests` | optional `Retry-After` header |
| [`ServiceUnavailableException`](ServiceUnavailableException.cs) | 503 | `…:service-unavailable` | optional `Retry-After` header |

All live in the `Simplic.OxS.Exceptions` namespace.

## Usage

### The common case

```csharp
using Simplic.OxS.Exceptions;

// 400 — malformed request or a business validation rule
throw new BadRequestException("startDate must be before endDate.");

// 409 — state conflict
throw new ConflictException("The document was modified by someone else.");

// 403 — authenticated but not allowed
throw new ForbiddenException("You may not delete organization-owned records.");
```

### Not found: pick anonymous vs. identified deliberately

This is the most important choice in this set.

```csharp
// Tenant-scoped read: "doesn't exist" and "exists but isn't yours" MUST be indistinguishable,
// so a caller cannot probe for foreign ids. Reveals nothing about the resource.
var order = await repository.GetForOrganizationAsync(id, ct);
if (order is null)
    throw new NotFoundException();

// Owner-verified / administrative lookup where echoing the missing id is fine:
var article = await repository.GetAsync(id, ct)
    ?? throw ResourceNotFoundException.FromType<Article>(id);

// Or the null-guard helper (throws ResourceNotFoundException when null, returns the value otherwise):
var article = ResourceNotFoundException.ExpectNotNull(await repository.GetAsync(id, ct), id);
```

> Rule of thumb: **default to the anonymous `NotFoundException` for organization-scoped reads.**
> Only use `ResourceNotFoundException` when the caller is already allowed to know the resource
> exists.

### Field-level validation errors (`errors` member)

`BadRequestException` (400) and `UnprocessableEntityException` (422) can carry a field-error map,
emitted under the `errors` member exactly like ASP.NET Core's `ValidationProblemDetails` (property
path → one or more messages). Use 400 for malformed/structural input and 422 for input that is
well-formed but violates semantic/business rules.

```csharp
// Single field
throw new BadRequestException("customer.name", "must not be empty");

// Fluent, multiple fields / multiple problems per field
throw new UnprocessableEntityException("order.total", "must be positive")
    .AddError("order.total", "exceeds the customer credit limit")
    .AddError("order.currency", "is not supported");

// From a pre-built map
var errors = new Dictionary<string, string[]>
{
    ["customer.name"] = new[] { "must not be empty" },
    ["customer.age"]  = new[] { "must be at least 18" },
};
throw new BadRequestException(errors);
```

Resulting body (400):

```json
{
  "type": "urn:simplic-oxs:problem:bad-request",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed for 'customer.name': must not be empty",
  "instance": "/api/v1/customer",
  "traceId": "00-…",
  "errors": { "customer.name": ["must not be empty"] }
}
```

> Note: model-state binding/validation failures on `[ApiController]` controllers are automatically
> converted into a `BadRequestException` by the framework, so every 400 shares this one contract.

### Rate limiting / unavailability with `Retry-After`

```csharp
// 429 — per-organization throttle; the hint is emitted as the Retry-After header (delta-seconds)
throw new TooManyRequestsException("Upload quota exceeded.", retryAfter: TimeSpan.FromSeconds(30));

// 503 — dependency temporarily down
throw new ServiceUnavailableException("Upstream is rate-limiting us.", retryAfter: TimeSpan.FromMinutes(1));
```

## Keeping a message log-only

`ProblemDetails.detail` is populated from the exception's `Detail`, which defaults to `Message`.
If a message is meant for logs only, override `Detail` to return a client-safe string:

```csharp
public sealed class PaymentDeclinedException : OxSException
{
    public PaymentDeclinedException(string gatewayReason)   // goes to the log
        : base(gatewayReason) { }

    public override int StatusCode => 402;
    public override string? Title => "Payment Required";
    public override string? ProblemType => "urn:simplic-oxs:problem:payment-declined";
    public override string? Detail => "The payment was declined.";   // client-safe
}
```

## Writing your own OxSException

Derive from [`OxSException`](OxSException.cs) and override what you need:

- `StatusCode` (required)
- `Title`, `ProblemType` (recommended — use a `urn:simplic-oxs:problem:<slug>` URN)
- `Detail` — override to keep `Message` log-only
- `PopulateProblemDetails(extensions)` — add structured, machine-readable body members
- `PopulateHeaders(headers)` — add response headers clients honour directly (e.g. `Retry-After`)

Prefer reusing the exceptions above. Only add a new type for a status/shape not already covered.

## Guidelines & conventions

- **Never** `BadRequest("… not found")` — throw a `NotFoundException` / `ResourceNotFoundException`.
- **Never** `Console.WriteLine` for errors — these exceptions are logged centrally.
- Problem-type URNs use the single scheme `urn:simplic-oxs:problem:*`. Service-specific extensions
  should follow `urn:simplic-oxs:problem:<service>-<slug>`.
- These types are namespace-consolidated under `Simplic.OxS.Exceptions`. The legacy
  `Simplic.OxS.ResourceNotFoundException` and `Simplic.OxS.Server.BadRequestException` are
  `[Obsolete]` shims kept for one deprecation cycle — do not use them in new code.

See the shared guidelines under `.agent-guidelines/guidelines/` (REST conventions and base classes)
for the fleet-wide rules these exceptions implement.
