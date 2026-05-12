# Repository instructions (Claude Code)

This file is auto-loaded by Claude Code at session start. The same
content is mirrored in `AGENTS.md` for other AI tools (Cursor, Aider,
Copilot, …).

## Before answering or generating code in this repository

1. Read `.agent-guidelines/AGENTS.md` — the index of the shared
   knowledge base every Simplic OxS API repo follows.
2. Pick relevant topic files from `.agent-guidelines/guidelines/`
   based on the task. The index in step 1 has a task-mapping table
   ("If the user says ... | Read these first").
3. Consult `.agent-guidelines/DECISIONS.md` whenever the fleet has
   multiple paradigms — the recorded winner is authoritative.
4. Before changing existing code, scan the file you're touching
   against `.agent-guidelines/guidelines/99-banned-patterns-and-legacy-migration.md`.
   Flag legacy deviations the user did not ask you to fix as a one-line
   "should we update this?" — never silently rewrite unrelated code.

## Hard rules (non-negotiable)

Full list and rationale: `.agent-guidelines/guidelines/99-banned-patterns-and-legacy-migration.md`.

- **Multi-tenancy is the default.** Every domain entity is
  organization-scoped (`OrganizationDocumentBase`).
- **Never return entities from controllers.** Always map to a DTO in
  `Server/Model/`.
- **Never `BadRequest("... not found")`.** The correct status is `NotFound()`.
- **Never `Console.WriteLine` in server code.** Use `ILogger<T>`.
- **Never commit secrets** to `appsettings.*.json`. Production secrets
  come from env vars / K8s.
- **Every controller action takes `CancellationToken ct`** and
  forwards it.
- **Never `async void` in tests.** Use `async Task`.
- **Never `Task.Delay(n)` as a synchronization mechanism** in
  production code paths.

## If `.agent-guidelines/.last-sync` is missing or older than today

The guidelines are stale. The Claude Code `SessionStart` hook should
have run the sync script automatically — if it didn't, run it
manually before continuing:

- Windows: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync-guidelines.ps1`
- Unix: `bash scripts/sync-guidelines.sh`

The script is idempotent and exits immediately on same-day re-runs.

## Repo-specific notes

_(Add anything specific to this project below — domain context, gotchas,
local conventions. Keep it short; everything cross-cutting belongs in
the central guidelines under `.agent-guidelines/`, not here.)_
