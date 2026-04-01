# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

GitLabSync is a .NET CLI tool that synchronizes files and folders from template repositories to target repositories in GitLab. It uses the NGitLab client library and is configured via YAML.

## Build and Test Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run all tests
dotnet test -c Release

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName" -c Release

# Run tests in the specific test project
dotnet test test/unit/GitSync.GitLab.Tests.csproj -c Release
```

Pre-commit hooks (via Husky.Net) automatically run `dotnet restore` and `dotnet build -c Release`. Pre-push hooks run the full test suite.

## Architecture

The project follows a layered provider-gateway pattern:

```
GitSync.GitLab.Tool  →  CLI entry point; reads env vars, loads YAML config, orchestrates sync
GitSync              →  Core sync engine; provider abstractions, diff logic, PR management
GitSync.GitLab       →  GitLab-specific IGitProviderGateway implementation via NGitLab
```

**Key source files:**
- [src/GitSync.GitLab.Tool/Program.cs](src/GitSync.GitLab.Tool/Program.cs) — reads `GitLab_OAuthToken` / `GitLab_HostUrl` env vars, loads YAML config
- [src/GitSync/Syncer.cs](src/GitSync/Syncer.cs) — compares source/target SHAs, creates branches/commits/PRs
- [src/GitSync/RepoSync.cs](src/GitSync/RepoSync.cs) — coordinates source and target repo lists, handles include/exclude modes and labels
- [src/GitSync.GitLab/GitLabGateway.cs](src/GitSync.GitLab/GitLabGateway.cs) — implements `IGitProviderGateway`; includes blob/tree/commit caching
- [src/GitSync/GitProvider/](src/GitSync/GitProvider/) — interface abstractions (`IGitProviderGateway` and related)
- [src/GitSync.GitLab.Tool/Config/](src/GitSync.GitLab.Tool/Config/) — `Context`, `Template`, `Repository`, `ContextLoader` (YAML deserialization)

**Test project:** [test/unit/](test/unit/) uses xUnit v3, NGitLab.Mock, and Verify (snapshot testing). Verified snapshots are `.verified.txt` files committed alongside tests.

## Key Conventions

- **Centralized package versions** in [Directory.Packages.props](Directory.Packages.props) — add new packages there, not in individual `.csproj` files.
- **ConfigureAwait.Fody** is active — do not manually add `.ConfigureAwait(false)` calls; the Fody weaver handles this at compile time.
- **Nullable reference types** are enabled globally via [Directory.Build.props](Directory.Build.props).
- **Snapshot tests** using Verify: run tests once to generate `.received.txt` files, then accept with `dotnet verify accept` or rename manually.
- Solution file uses the modern `.slnx` format ([GitLabSync.slnx](GitLabSync.slnx)).

## GitHub CLI

This repo is hosted on GitHub. Use `gh` for source control operations (issues, PRs, etc.).
