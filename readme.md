# GitLabSync

![Build Status](https://github.com/jasonwoods-7/GitLabSync/actions/workflows/dotnet_ci.yml/badge.svg)
[![NuGet Status](https://img.shields.io/nuget/v/GitLabSync.Tool.svg?label=GitLabSync.Tool)](https://www.nuget.org/packages/GitLabSync.Tool/)

A tool to help synchronize specific files and folders across repositories in GitLab.

This tool is based on [GitHubSync](https://github.com/SimonCropp/GitHubSync) by Simon Cropp.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## dotnet tool

This tool reads its configuration from a YAML file, allowing customization of templates and repositories without recompiling.

### Installation

```bash
dotnet tool install -g GitLabSync.Tool
```

### Usage

Set the `GitLab_OAuthToken` environment variable to a personal access token before running.

Optionally set `GitLab_HostUrl` to the base URL of your GitLab instance (defaults to `https://gitlab.com`).

Run against `gitlabsync.yaml` in the current directory:

```bash
gitlabsync
```

Run against a specific config file:

```bash
gitlabsync /path/to/gitlabsync.yaml
```

### Configuration

```yaml
templates:
  - name: [template name]
    url: [repository url of the template]
    branch: [branch to use, defaults to `main`]
    ignore:
      - [optional list of file paths to exclude from sync]

repositories:
  - name: [repository name]
    url: [repository url of the target repository]
    branch: [target branch, defaults to `master`]
    auto_merge: [optional bool; if true, merges the sync PR automatically]
    templates:
      - [list of template names to apply in order]
    labels:
      - [optional list of labels to apply to the sync pull request]
```

## Project Structure

| Path | Purpose |
|------|---------|
| `src/GitSync.GitLab.Tool/` | CLI entry point; reads env vars, loads YAML config, orchestrates sync |
| `src/GitSync/` | Core sync engine; provider abstractions, diff logic, PR management |
| `src/GitSync.GitLab/` | GitLab-specific implementation via NGitLab |
| `test/unit/` | xUnit v3 tests using NGitLab.Mock and Verify snapshot testing |

## Building and Testing

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run all tests
dotnet test -c Release

# Run tests in the unit test project
dotnet test --project test/unit/GitSync.GitLab.Tests.csproj -c Release
```

Pre-commit hooks (via Husky.Net) automatically run `dotnet restore` and `dotnet build -c Release`. Pre-push hooks run the full test suite.

## License

[MIT](LICENSE)
