using GitSync.GitProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NGitLab;
using NGitLab.Mock.Config;

namespace GitSync.GitLab.Tests;

public class RepoSyncTests
{
    [Fact]
    public async Task IgnorePathsExcludesIgnoredFilesFromSync()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/source",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c =>
                            c.WithFile("readme.md", "Hello, World!")
                                .WithFile("ignored.txt", "Ignore me!")
                    )
            )
            .WithProjectOfFullPath(
                "group/target",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("placeholder.txt", "placeholder")
                    )
            )
            .BuildServer();

        var credentials = new TestCredentials(server.CreateClient());
        var sourceInfo = new RepositoryInfo(
            credentials,
            "group",
            "source",
            "main",
            new HashSet<string>(["ignored.txt"], StringComparer.Ordinal)
        );
        var targetInfo = new RepositoryInfo(
            credentials,
            "group",
            "target",
            "main",
            new HashSet<string>(StringComparer.Ordinal)
        );

        var sync = new RepoSync(NullLogger.Instance);
        sync.AddSourceRepository(sourceInfo);
        sync.AddTargetRepository(targetInfo);

        // Act
        var context = await sync.CalculateSyncContext(targetInfo);

        // Assert
        var sourcePaths = context
            .Diff.ToBeAddedOrUpdatedEntries.Select(kvp => kvp.Key.Path)
            .ToList();
        sourcePaths.ShouldContain("readme.md");
        sourcePaths.ShouldNotContain("ignored.txt");
    }

    sealed class TestCredentials(IGitLabClient client) : ICredentials
    {
        public IGitProviderGateway CreateGateway(System.Net.IWebProxy? webProxy, ILogger logger) =>
            new GitLabGateway(client, logger);

        public LibGit2Sharp.Credentials CreateLibGit2SharpCredentials() =>
            throw new NotSupportedException();
    }
}
