using GitSync.GitProvider;

namespace GitSync;

public class RepositoryInfo(
    ICredentials credentials,
    string owner,
    string repository,
    string branch,
    IReadOnlySet<string> ignorePaths
)
{
    public ICredentials Credentials { get; } = credentials;
    public string Owner { get; } = owner;
    public string Repository { get; } = repository;
    public string Branch { get; } = branch;
    public IReadOnlySet<string> IgnorePaths { get; } = ignorePaths;
}
