#!/usr/bin/env pwsh
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
#   -Force   ignore the timestamp and re-sync now

[CmdletBinding()]
param(
    [string]$Repo   = $(if ($env:AGENT_GUIDELINES_REPO)   { $env:AGENT_GUIDELINES_REPO }   else { 'git@github.com:simplic/simplic-oxs-agent-skill.git' }),
    [string]$Dir    = $(if ($env:AGENT_GUIDELINES_DIR)    { $env:AGENT_GUIDELINES_DIR }    else { '.agent-guidelines' }),
    [string]$Branch = $(if ($env:AGENT_GUIDELINES_BRANCH) { $env:AGENT_GUIDELINES_BRANCH } else { 'master' }),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$GitArgs)
    & git @GitArgs 2>$null
    return $LASTEXITCODE -eq 0
}

function Test-GitAvailable {
    & git --version >$null 2>&1
    return $LASTEXITCODE -eq 0
}

$stamp = Join-Path $Dir '.last-sync'
$today = Get-Date -Format 'yyyy-MM-dd'

if (-not $Force -and (Test-Path $stamp)) {
    $last = (Get-Content -Path $stamp -Raw -ErrorAction SilentlyContinue).Trim()
    if ($last -eq $today) { exit 0 }
}

$gitAvailable = Test-GitAvailable
$hasLocalGit = Test-Path (Join-Path $Dir '.git')

if ($gitAvailable) {
    if ($hasLocalGit) {
        # Try to fetch updates from remote
        if (Invoke-Git @('-C', $Dir, 'fetch', '--quiet', 'origin', $Branch)) {
            Invoke-Git @('-C', $Dir, 'reset', '--quiet', '--hard', "origin/$Branch") | Out-Null
            Set-Content -Path $stamp -Value $today -Encoding utf8 -NoNewline
            Write-Host "[agent-guidelines] synced from $Repo ($Branch) -> $Dir @ $today"
        } else {
            Write-Host "[agent-guidelines] warning: git fetch failed, using local copy in $Dir"
        }
    } else {
        # Clone the repo for the first time
        if (Test-Path $Dir) { Remove-Item -Recurse -Force $Dir }
        if (Invoke-Git @('clone', '--quiet', '--depth', '1', '--branch', $Branch, $Repo, $Dir)) {
            Set-Content -Path $stamp -Value $today -Encoding utf8 -NoNewline
            Write-Host "[agent-guidelines] synced from $Repo ($Branch) -> $Dir @ $today"
        } else {
            Write-Host "[agent-guidelines] warning: git clone failed"
        }
    }
} else {
    # Git not available; use local copy if it exists
    if (-not (Test-Path $Dir)) {
        Write-Host "[agent-guidelines] error: git not available and $Dir does not exist"
        exit 1
    }
    Write-Host "[agent-guidelines] git not available, using local copy from $Dir"
}
