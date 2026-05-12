# GitHub Copilot — repository instructions

This repository follows shared agent guidelines from the central
`simplic-oxs-agent-skill` repo.

## Before generating code or answering, read

1. `AGENTS.md` at the repo root (always).
2. The relevant topic file under `.agent-guidelines/guidelines/` for
   the task at hand. The index lives in `.agent-guidelines/AGENTS.md`.
3. `.agent-guidelines/DECISIONS.md` whenever the fleet has multiple
   paradigms — use the recorded winner, not local consistency.

## Hard rules (non-negotiable)

Full list and rationale: `.agent-guidelines/guidelines/99-banned-patterns-and-legacy-migration.md`.

- Never `BadRequest("... not found")` — use `NotFound()`.
- Never `Console.WriteLine` in server code — inject `ILogger<T>`.
- Never `Task.Delay(n)` as a synchronization mechanism in production.
- Never `async void` in tests — use `async Task`.
- Every controller action takes `CancellationToken ct` and forwards it.
- Never return domain entities from controllers — map to a DTO first.
- Never commit secrets; production secrets come from env vars / K8s.

## On legacy code the user did not ask you to fix

Flag deviations with a one-line "should we update this?" — never
silently rewrite unrelated code.

## If guidelines look stale

If `.agent-guidelines/.last-sync` is missing or older than today, refresh
before answering:

- Windows: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync-guidelines.ps1`
- Unix: `bash scripts/sync-guidelines.sh`
