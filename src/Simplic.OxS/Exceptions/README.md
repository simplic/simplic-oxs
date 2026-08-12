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
| [`NotFoundException`](NotFoundException.cs) | 404 | `…:not-found` | **preferred** — anonymous, no resource info |
| [`ResourceNotFoundException`](ResourceNotFoundException.cs) | 404 | `…:not-found` | **deprecated** — `resource` / `resourceType` / `resourceId` |
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

### Not found: always prefer the anonymous `NotFoundException`

```csharp
// Preferred everywhere: reveals nothing — "doesn't exist", "isn't yours" and "invalid route"
// are indistinguishable, so a caller cannot probe for foreign ids.
var order = await repository.GetForOrganizationAsync(id, ct);
if (order is null)
    throw new NotFoundException();
```

`ResourceNotFoundException` is **deprecated** because echoing the resource type and id leaks whether
a resource exists. It remains only for administrative/owner-verified lookups that already prove the
caller may know the resource, and will be removed in a future major version:

```csharp
// Deprecated — only for lookups where the caller is already allowed to know the resource exists.
#pragma warning disable CS0618
var article = await repository.GetAsync(id, ct)
    ?? throw ResourceNotFoundException.FromType<Article>(id);
#pragma warning restore CS0618
```

> Rule of thumb: **use the anonymous `NotFoundException` for every not-found.** Reach for
> `ResourceNotFoundException` only in an administrative context, and expect it to go away.

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

## Handling third-party exceptions

Exceptions from libraries you don't own (a database driver, an HTTP/SDK client, a payment gateway)
don't derive from `OxSException`, so by default they fall through the handler chain to the
**`FallbackExceptionHandler`** → a generic **500** in production (the developer exception page in
Development/Staging/Local). That's safe — no internal detail leaks — but it carries no meaningful
status or problem-type. Map them into the format in one of two ways.

### 1. Translate at the boundary (occasional call sites)

Catch the third-party exception where you call it and rethrow an `OxSException`, keeping the original
as the inner exception so it's still logged:

```csharp
try
{
    await paymentGateway.ChargeAsync(request, ct);
}
catch (StripeException ex)
{
    // Client gets a clean problem+json; the StripeException is preserved for the log.
    throw new ServiceUnavailableException("Payment provider is unavailable.", innerException: ex);
}
```

> Bonus: `OxSExceptionHandler` unwraps inner exceptions (`ExceptionUnpacker`), so if a third-party
> wrapper exception already *contains* an `OxSException` in its `InnerException` chain, it is
> surfaced correctly with no extra work.

### 2. A dedicated `IExceptionHandler` (a whole library, cross-cutting)

When a library throws the same exception types across many call sites, map them once in a global
handler — this is exactly what the built-in
[`FrameworkExceptionHandler`](../../Simplic.OxS.Server/Exceptions/Handlers/FrameworkExceptionHandler.cs)
does for Kestrel/multipart exceptions. Add your own handler and register it in `Bootstrap` **before**
the `FallbackExceptionHandler`:

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Simplic.OxS.Server.Exceptions;   // ProblemDetailsResponseWriter

public sealed class MongoExceptionHandler(ILogger<MongoExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        var mapped = exception switch
        {
            MongoConnectionException   => (503, "Storage is temporarily unavailable.", "urn:simplic-oxs:problem:service-unavailable"),
            MongoDuplicateKeyException => (409, "The record already exists.",           "urn:simplic-oxs:problem:conflict"),
            _ => ((int Status, string Detail, string Type)?)null
        };

        if (mapped is null)
            return false;   // not ours — let the next handler try

        var (status, detail, type) = mapped.Value;
        logger.LogWarning(exception, "Mapped {ExceptionType} to {Status}", exception.GetType().Name, status);

        var problem = ProblemDetailsResponseWriter.Create(ctx, status, title: null, type: type, detail);
        await ProblemDetailsResponseWriter.WriteAsync(ctx, problem, headers: null, ct);
        return true;
    }
}
```

Register it (order matters — first handler that returns `true` wins, and the fallback must stay last):

```csharp
services.AddExceptionHandler<MongoExceptionHandler>();   // before AddExceptionHandler<FallbackExceptionHandler>()
```

Return `false` for anything you don't recognise so the exception continues down the chain to the
fallback. Never put a third-party exception's raw `Message` into `detail` unless you're sure it's
client-safe — translate it to a fixed, non-leaking string.

## Guidelines & conventions

- **Never** `BadRequest("… not found")` — throw a `NotFoundException` (preferred, anonymous).
- **Never** `Console.WriteLine` for errors — these exceptions are logged centrally.
- Problem-type URNs use the single scheme `urn:simplic-oxs:problem:*`. Service-specific extensions
  should follow `urn:simplic-oxs:problem:<service>-<slug>`.
- These types are namespace-consolidated under `Simplic.OxS.Exceptions`. The legacy
  `Simplic.OxS.ResourceNotFoundException` and `Simplic.OxS.Server.BadRequestException` are
  `[Obsolete]` shims kept for one deprecation cycle — do not use them in new code.

See the shared guidelines under `.agent-guidelines/guidelines/` (REST conventions and base classes)
for the fleet-wide rules these exceptions implement.
