using GitSync.GitProvider;
using NGitLab.Mock.Config;
using NGitLab.Models;
using VerifyTests.MicrosoftLogging;

namespace GitSync.GitLab.Tests;

public class GitLabGatewayTests
{
    const string HelloWorldSha = "b45ef6fec89518d314f546fd6c3025367b721684";

    [Fact]
    public async Task GetCurrentUser()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig().WithUser("user", isDefault: true).BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var user = await gateway.GetCurrentUser();

        // Assert
        await Verify(user);
    }

    [Theory]
    [InlineData(AccessLevel.NoAccess)]
    [InlineData(AccessLevel.Guest)]
    [InlineData(AccessLevel.Reporter)]
    [InlineData(AccessLevel.Developer)]
    [InlineData(AccessLevel.Maintainer)]
    [InlineData(AccessLevel.Owner)]
    [InlineData(AccessLevel.Admin)]
    public async Task IsCollaboratorWithProjectAccess(AccessLevel accessLevel)
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithProjectOfFullPath(
                "group/project",
                configure: p => p.WithUserPermission("user", accessLevel)
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        await Verify(isCollaborator);
    }

    [Theory]
    [InlineData(AccessLevel.NoAccess)]
    [InlineData(AccessLevel.Guest)]
    [InlineData(AccessLevel.Reporter)]
    [InlineData(AccessLevel.Developer)]
    [InlineData(AccessLevel.Maintainer)]
    [InlineData(AccessLevel.Owner)]
    [InlineData(AccessLevel.Admin)]
    public async Task IsCollaboratorWithGroupAccess(AccessLevel accessLevel)
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithGroupOfFullPath("group", configure: g => g.WithUserPermission("user", accessLevel))
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        await Verify(isCollaborator);
    }

    [Fact]
    public async Task IsCollaboratorWithNoAccess()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user")
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        await Verify(isCollaborator);
    }

    [Fact]
    public async Task Fork()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var fork = await gateway.Fork("group", "project");

        // Assert
        fork.ShouldNotBeNull();
        await Verify();
    }

    [Fact]
    public async Task DownloadBlob()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        using var targetStream = new MemoryStream();

        // Act
        await gateway.DownloadBlob(
            new("group", "project", TreeEntryTargetType.Blob, "main", "readme.md", HelloWorldSha),
            targetStream
        );

        // Assert
        await Verify(targetStream.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other title")]
    [InlineData("title")]
    public async Task HasOpenPullRequests(string? title)
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                configure: p =>
                {
                    if (title is not null)
                    {
                        p.WithMergeRequest(title: title);
                    }
                }
            )
            .BuildServer();
        var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var hasOpenPullRequests = await gateway.HasOpenPullRequests("group", "project", "title");

        // Assert
        await Verify(hasOpenPullRequests);
    }

    [Fact]
    public async Task RootCommitFrom()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                id: 1,
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                            "Initial commit",
                            configure: c => c.WithFile("readme.md", "Hello, World!")
                        )
                        .WithCommit(
                            "Second commit",
                            configure: c => c.WithFile("subFolder/hello.txt", "Hello, World!")
                        )
            )
            .BuildServer();
        var client = server.CreateClient();
        using var gateway = new GitLabGateway(client, provider.CreateLogger<GitLabGateway>());

        // Act
        var commit = await gateway.RootCommitFrom(
            new("group", "project", TreeEntryTargetType.Tree, "main", null)
        );

        // Assert
        commit.Sha.ShouldMatch("[A-F0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task TreeFromDoNotThrowExists()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c =>
                            c.WithFile("readme.md", "my readme")
                                .WithFile("subFolder/hello.txt", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var tree = await gateway.TreeFrom(
            new("group", "project", TreeEntryTargetType.Tree, "main", null),
            false
        );

        // Assert
        await Verify(tree!.Item2);
    }

    [Fact]
    public async Task TreeFromDoNotThrowExistsInSubDir()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c =>
                            c.WithFile("readme.md", "my readme")
                                .WithFile("subFolder/hello.txt", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var tree = await gateway.TreeFrom(
            new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"),
            false
        );

        // Assert
        await Verify(tree!.Item2);
    }

    [Fact]
    public async Task TreeFromDoNotThrowDoesNotExist()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var tree = await gateway.TreeFrom(
            new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"),
            false
        );

        // Assert
        await Verify(tree);
    }

    [Fact]
    public async Task TreeFromThrowsDoesNotExist()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var tree = await gateway.TreeFrom(
            new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"),
            true
        );

        // Assert
        await Verify(tree);
    }

    [Fact]
    public async Task BlobFromDoNotThrowExists()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var blob = await gateway.BlobFrom(
            new("group", "project", TreeEntryTargetType.Blob, "main", "readme.md"),
            false
        );

        // Assert
        await Verify(blob!.Item2);
    }

    [Fact]
    public async Task BlobFromDoNotThrowExistsInSubDir()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c =>
                            c.WithFile("readme.md", "Hello, World!")
                                .WithFile("subFolder/hello.txt", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var blob = await gateway.BlobFrom(
            new("group", "project", TreeEntryTargetType.Blob, "main", "subFolder/hello.txt"),
            false
        );

        // Assert
        await Verify(blob!.Item2);
    }

    [Fact]
    public async Task BlobFromDoNotThrowDoesNotExist()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var blob = await gateway.BlobFrom(
            new("group", "project", TreeEntryTargetType.Blob, "main", "hello.txt"),
            false
        );

        // Assert
        await Verify(blob);
    }

    [Fact]
    public async Task BlobFromThrowsDoesNotExist()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var handler = () =>
            // ReSharper disable once AccessToDisposedClosure
            gateway.BlobFrom(
                new("group", "project", TreeEntryTargetType.Blob, "main", "hello.txt"),
                true
            );

        // Assert
        await Verify(await handler.ShouldThrowAsync<Exception>());
    }

    [Fact]
    public async Task CreateCommit()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        // Act
        var commit = await gateway.CreateCommit(
            "treeSha",
            "group",
            "project",
            "parentSha",
            "branch",
            "chore(sync): gitlab sync"
        );

        // Assert
        commit.ShouldMatch("[A-F0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task CreateTreeEmpty()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);

        // Act
        var treeSha = await gateway.CreateTree(newTree, "group", "project");

        // Assert
        await Verify(treeSha);
    }

    [Fact]
    public async Task CreateTreeEmptyBlob()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                configure: project =>
                    project.WithCommit(
                        "Initial commit",
                        configure: commit => commit.WithFile("directory/.gitkeep")
                    )
            )
            .BuildServer();
        var client = server.CreateClient();
        using var gateway = new GitLabGateway(client, provider.CreateLogger<GitLabGateway>());

        var repository = client.GetRepository(1);
        var gitKeep = repository.GetTreeAsync(new() { Path = "directory" }).Single();

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add(gitKeep.Mode, gitKeep.Name, gitKeep.Id.ToString(), TreeType.Blob);

        // Act
        var treeSha = await gateway.CreateTree(newTree, "group", "project");

        // Assert
        var directory = repository.GetTreeAsync(new()).Single();
        treeSha.ShouldBeEquivalentTo(directory.Id.ToString());
    }

    [Fact]
    public async Task CreateBlob()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        await gateway.FetchBlob("group", "project", HelloWorldSha);

        // Act
        await gateway.CreateBlob("group", "project", HelloWorldSha);

        // Assert
        await Verify(gateway.IsKnownBy<IBlob>(HelloWorldSha, "group", "project"));
    }

    [Fact]
    public async Task CreateBranchNewFileNotExecutable()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "group",
            "project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.ShouldMatch("[a-f0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task CreateBranchNewFileExecutable()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);
        var subTree = gateway.CreateNewTree("bin");
        var emptyTree = gateway.CreateNewTree("empty");
        subTree.Tree.Add("100755", "hello.sh", HelloWorldSha, TreeType.Blob);
        newTree.Tree.Add(
            "040000",
            "bin",
            await gateway.CreateTree(subTree, "group", "project"),
            TreeType.Tree
        );
        newTree.Tree.Add(
            "040000",
            "empty",
            await gateway.CreateTree(emptyTree, "group", "project"),
            TreeType.Tree
        );
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "group",
            "project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.ShouldMatch("[a-f0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task CreateBranchUpdatedFileNotExecutable()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, world!")
                    )
            )
            .WithProjectOfFullPath(
                "group/project2",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "readme.md", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project2", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "group",
            "project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.ShouldMatch("[a-f0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task CreateBranchUpdatedFileExecutable()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var _ = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                            "Initial commit",
                            configure: c => c.WithFile("readme.md", "Hello, world!")
                        )
                        .WithCommit(
                            "Add script",
                            configure: c => c.WithFile("bin/hello.sh", "Hello, World!")
                        )
            )
            .WithProjectOfFullPath(
                "group/project2",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Add script",
                        configure: c => c.WithFile("bin/hello.sh", "echo 'Hello, World!'")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var sourceTree = await gateway.TreeFrom(
            new("group", "project2", TreeEntryTargetType.Tree, "main", "bin"),
            false
        );
        var scriptSha = sourceTree!.Item2.Tree.Single().Sha;
        await gateway.FetchBlob("group", "project2", scriptSha);

        var newTree = gateway.CreateNewTree("bin");
        newTree.Tree.Add("100755", "hello.sh", scriptSha, TreeType.Blob);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "group",
            "project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.ShouldMatch("[a-f0-9]{40}");
        await Verify();
    }

    [Fact]
    public async Task CreatePullRequest()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var recording = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "owner/group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("owner", "group/project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "owner", "group/project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "owner",
            "group/project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        _ = await gateway.CreateBranch("owner", "group/project", "branch", commitId);

        // Act
        var id = await gateway.CreatePullRequest(
            "owner",
            "group/project",
            "branch",
            "main",
            false,
            "GitLabSync",
            null
        );

        // Assert
        await Verify(id);
    }

    [Fact]
    public async Task ApplyLabels()
    {
        // Arrange
        var provider = new RecordingProvider();

        using var recording = Recording.Start();

        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath(
                "group/project",
                addDefaultUserAsMaintainer: true,
                configure: p =>
                    p.WithCommit(
                        "Initial commit",
                        configure: c => c.WithFile("readme.md", "Hello, World!")
                    )
            )
            .BuildServer();
        using var gateway = new GitLabGateway(
            server.CreateClient(),
            provider.CreateLogger<GitLabGateway>()
        );

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(
            treeId,
            "group",
            "project",
            "main",
            "main",
            "chore(sync): gitlab sync"
        );

        _ = await gateway.CreateBranch("group", "project", "branch", commitId);

        var id = await gateway.CreatePullRequest(
            "group",
            "project",
            "branch",
            "main",
            false,
            "GitLabSync",
            null
        );

        // Act
        var labels = await gateway.ApplyLabels("group", "project", id, ["label"]);

        // Assert
        await Verify(labels);
    }
}
