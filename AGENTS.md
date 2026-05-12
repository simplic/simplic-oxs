# Agent Instructions

This repository follows the shared **Simplic OXS** agent guidelines. Those
guidelines do not live in this repo — they are fetched on demand from the
central `simplic-oxs-agent-skill` repo into a local, gitignored folder.

## What every AI agent must do here

1. **Ensure the guidelines are present and fresh.** Check whether
   `.agent-guidelines/.last-sync` exists and contains today's date
   (`YYYY-MM-DD`). If it is missing, empty, or older than today:

       # Windows / cross-platform
       pwsh -NoProfile -File scripts/sync-guidelines.ps1

       # Unix
       ./scripts/sync-guidelines.sh

   The script is idempotent: same-day reruns exit immediately without network
   I/O, so it is always safe to invoke.

2. **Read the synced guidelines before changing code.** Start with
   `.agent-guidelines/AGENTS.md`, then follow links from there into
   `.agent-guidelines/guidelines/`.

3. **Treat the synced files as read-only.** They are owned by the
   `simplic-oxs-agent-skill` repo. To change a guideline, open a PR there.

## Why this indirection?

- **Single source of truth.** All Simplic OXS API repos consume the same
  guidelines without copy-paste drift.
- **Cheap freshness check.** The script-driven, once-per-day sync uses zero AI
  tokens and one shallow `git fetch`.
- **AI-agnostic.** Works with any agent that respects the
  [`AGENTS.md`](https://agents.md) convention — Claude Code, Cursor, Aider,
  GitHub Copilot Chat, Continue, etc.

## Repo-specific notes

_(Add anything specific to this project below — domain context, gotchas, local
conventions. Keep it short; everything cross-cutting belongs in the central
repo, not here.)_
