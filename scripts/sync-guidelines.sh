#!/usr/bin/env bash
# Sync shared agent guidelines from the central simplic-oxs-agent-skill repo.
#
# Idempotent + cheap: returns immediately (exit 0, no network) if already
# synced today. Designed to be invoked from a per-AI session-start hook OR
# manually before/while interacting with any AI agent.
#
# Configuration (env vars override defaults):
#   AGENT_GUIDELINES_REPO    git URL of the central repo
#   AGENT_GUIDELINES_DIR     local sync target  (default: .agent-guidelines)
#   AGENT_GUIDELINES_BRANCH  branch to track    (default: master)
#
# Flags:
#   --force  ignore the timestamp and re-sync now

set -eu

REPO="${AGENT_GUIDELINES_REPO:-git@github.com:simplic/simplic-oxs-agent-skill.git}"
DIR="${AGENT_GUIDELINES_DIR:-.agent-guidelines}"
BRANCH="${AGENT_GUIDELINES_BRANCH:-master}"
FORCE="${1:-}"

STAMP="$DIR/.last-sync"
TODAY="$(date +%Y-%m-%d)"

if [[ "$FORCE" != "--force" ]] && [[ -f "$STAMP" ]] && [[ "$(cat "$STAMP")" == "$TODAY" ]]; then
    exit 0
fi

# Check if git is available
GIT_AVAILABLE=0
if command -v git >/dev/null 2>&1; then
    GIT_AVAILABLE=1
fi

if [[ $GIT_AVAILABLE -eq 1 ]]; then
    if [[ -d "$DIR/.git" ]]; then
        # Try to fetch updates; continue even if it fails
        if git -C "$DIR" fetch --quiet origin "$BRANCH" 2>/dev/null && git -C "$DIR" reset --quiet --hard "origin/$BRANCH" 2>/dev/null; then
            printf '%s' "$TODAY" > "$STAMP"
            echo "[agent-guidelines] synced from $REPO ($BRANCH) -> $DIR @ $TODAY"
        else
            echo "[agent-guidelines] warning: git fetch failed, using local copy in $DIR"
        fi
    else
        # Clone the repo for the first time
        rm -rf "$DIR"
        if git clone --quiet --depth 1 --branch "$BRANCH" "$REPO" "$DIR" 2>/dev/null; then
            printf '%s' "$TODAY" > "$STAMP"
            echo "[agent-guidelines] synced from $REPO ($BRANCH) -> $DIR @ $TODAY"
        else
            echo "[agent-guidelines] warning: git clone failed"
        fi
    fi
else
    # Git not available; use local copy if it exists
    if [[ ! -d "$DIR" ]]; then
        echo "[agent-guidelines] error: git not available and $DIR does not exist"
        exit 1
    fi
    echo "[agent-guidelines] git not available, using local copy from $DIR"
fi
