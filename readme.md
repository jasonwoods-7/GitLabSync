# GitLabSync

A tool to help synchronize specific files and folders across repositories in GitLab.

This tool is based on [GitHubSync](https://github.com/SimonCropp/GitHubSync) by Simon Cropp.

## dotnet tool

This tool allows reading the configuration from a file. This allows customization of the templates and repositories
without needing to recompile any code.

### Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

Install [GitLabSync.Tool](https://nuget.org/packages/GitLabSync.Tool/) globally.

```ps
dotnet tool install -g GitLabSync.Tool
```

### Usage

Run against the current directory will use `gitlabsync.yaml` in the current directory:

```ps
gitlabsync
```

Run against a specific file:

```ps
gitlabsync /path/to/gitlabsync.yaml
```

### Configuration definition

The configuration format is yaml. There should be 1 to n number of templates and 1 to n number of (target) repositories.

```yaml
templates:
  - name: [template name]
    url: [repository url of the template]
    branch: [branch to use, defaults to `main`]
    
repositories:
  - name: [repository name]
    url: [repository url of the target repository]
    branch: [target branch, defaults to `main`]
    templates:
      - [list of template names to use in the order to apply]
```
