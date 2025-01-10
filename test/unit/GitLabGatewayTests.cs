using GitSync.GitProvider;
using NGitLab.Mock.Config;
using NGitLab.Models;

namespace GitSync.GitLab.Tests;

public class GitLabGatewayTests(ITestOutputHelper output)
{
    const string HelloWorldSha = "b45ef6fec89518d314f546fd6c3025367b721684";
    const string EmptyTreeSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    readonly Action<string> writeLine = output.WriteLine;

    [Fact]
    public async Task GetCurrentUser()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var user = await gateway.GetCurrentUser();

        // Assert
        user.Should().NotBeNull();
    }

    [Theory]
    [InlineData(AccessLevel.NoAccess, false)]
    [InlineData(AccessLevel.Guest, false)]
    [InlineData(AccessLevel.Reporter, false)]
    [InlineData(AccessLevel.Developer, true)]
    [InlineData(AccessLevel.Maintainer, true)]
    [InlineData(AccessLevel.Owner, true)]
    [InlineData(AccessLevel.Admin, true)]
    public async Task IsCollaboratorWithProjectAccess(AccessLevel accessLevel, bool expected)
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithProjectOfFullPath("group/project", configure: p => p.WithUserPermission("user", accessLevel))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        isCollaborator.Should().Be(expected);
    }

    [Theory]
    [InlineData(AccessLevel.NoAccess, false)]
    [InlineData(AccessLevel.Guest, false)]
    [InlineData(AccessLevel.Reporter, false)]
    [InlineData(AccessLevel.Developer, true)]
    [InlineData(AccessLevel.Maintainer, true)]
    [InlineData(AccessLevel.Owner, true)]
    [InlineData(AccessLevel.Admin, true)]
    public async Task IsCollaboratorWithGroupAccess(AccessLevel accessLevel, bool expected)
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithGroupOfFullPath("group", configure: g => g.WithUserPermission("user", accessLevel))
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        isCollaborator.Should().Be(expected);
    }

    [Fact]
    public async Task IsCollaboratorWithNoAccess()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user")
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var isCollaborator = await gateway.IsCollaborator("group", "project");

        // Assert
        isCollaborator.Should().BeFalse();
    }

    [Fact]
    public async Task Fork()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user")
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var fork = await gateway.Fork("group", "project");

        // Assert
        fork.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadBlob()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        using var targetStream = new MemoryStream();

        // Act
        await gateway.DownloadBlob(new("group", "project", TreeEntryTargetType.Blob, "main", "readme.md", HelloWorldSha), targetStream);

        // Assert
        targetStream.Should().NotHavePosition(0L);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("other title", false)]
    [InlineData("title", true)]
    public async Task HasOpenPullRequests(string? title, bool expected)
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", configure: p =>
            {
                if (title is not null)
                {
                    p.WithMergeRequest(title: title);
                }
            })
            .BuildServer();
        var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var hasOpenPullRequests = await gateway.HasOpenPullRequests("group", "project", "title");

        // Assert
        hasOpenPullRequests.Should().Be(expected);
    }

    [Fact]
    public async Task RootCommitFrom()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", id: 1, addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!"))
                .WithCommit("Second commit", configure: c => c.WithFile("subFolder/hello.txt", "Hello, World!")))
            .BuildServer();
        var client = server.CreateClient();
        using var gateway = new GitLabGateway(client, this.writeLine);

        // Act
        var commit = await gateway.RootCommitFrom(new("group", "project", TreeEntryTargetType.Tree, "main", null));

        // Assert
        commit.Should().NotBeNull();
    }

    [Fact]
    public async Task TreeFromDoNotThrowExists()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c
                    .WithFile("readme.md", "my readme")
                    .WithFile("subFolder/hello.txt", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var tree = await gateway.TreeFrom(new("group", "project", TreeEntryTargetType.Tree, "main", null), false);

        // Assert
        await Verify(tree!.Item2);
    }

    [Fact]
    public async Task TreeFromDoNotThrowExistsInSubDir()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c
                    .WithFile("readme.md", "my readme")
                    .WithFile("subFolder/hello.txt", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var tree = await gateway.TreeFrom(new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"), false);

        // Assert
        await Verify(tree!.Item2);
    }

    [Fact]
    public async Task TreeFromDoNotThrowDoesNotExist()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var tree = await gateway.TreeFrom(new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"), false);

        // Assert
        tree.Should().NotBeNull();
        tree!.Item2.Tree.Should().BeEmpty();
    }

    [Fact]
    public async Task TreeFromThrowsDoesNotExist()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var tree = await gateway.TreeFrom(new("group", "project", TreeEntryTargetType.Tree, "main", "subFolder"), true);

        // Assert
        tree.Should().NotBeNull();
        tree!.Item2.Tree.Should().BeEmpty();
    }

    [Fact]
    public async Task BlobFromDoNotThrowExists()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var blob = await gateway.BlobFrom(new("group", "project", TreeEntryTargetType.Blob, "main", "readme.md"), false);

        // Assert
        await Verify(blob!.Item2);
    }

    [Fact]
    public async Task BlobFromDoNotThrowExistsInSubDir()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c
                    .WithFile("readme.md", "Hello, World!")
                    .WithFile("subFolder/hello.txt", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var blob = await gateway.BlobFrom(new("group", "project", TreeEntryTargetType.Blob, "main", "subFolder/hello.txt"), false);

        // Assert
        await Verify(blob!.Item2);
    }

    [Fact]
    public async Task BlobFromDoNotThrowDoesNotExist()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var blob = await gateway.BlobFrom(new("group", "project", TreeEntryTargetType.Blob, "main", "hello.txt"), false);

        // Assert
        blob.Should().BeNull();
    }

    [Fact]
    public async Task BlobFromThrowsDoesNotExist()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var handler = () => gateway.BlobFrom(new("group", "project", TreeEntryTargetType.Blob, "main", "hello.txt"), true);

        // Assert
        await handler.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateCommit()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        // Act
        var commit = await gateway.CreateCommit("treeSha", "group", "project", "parentSha", "branch", "chore(sync): gitlab sync");

        // Assert
        commit.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTreeEmpty()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project")
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);

        // Act
        var treeSha = await gateway.CreateTree(newTree, "group", "project");

        // Assert
        treeSha.Should().BeEquivalentTo(EmptyTreeSha);
    }

    [Fact]
    public async Task CreateTreeEmptyBlob()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", configure: project => project
                .WithCommit("Initial commit", configure: commit => commit
                    .WithFile("directory/.gitkeep")))
            .BuildServer();
        var client = server.CreateClient();
        using var gateway = new GitLabGateway(client, this.writeLine);

        var repository = client.GetRepository(1);
        var gitKeep = repository.GetTreeAsync(new() { Path = "directory" }).Single();

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add(gitKeep.Mode, gitKeep.Name, gitKeep.Id.ToString(), TreeType.Blob);

        // Act
        var treeSha = await gateway.CreateTree(newTree, "group", "project");

        // Assert
        var directory = repository.GetTreeAsync(new()).Single();
        treeSha.Should().BeEquivalentTo(directory.Id.ToString());
    }

    [Fact]
    public async Task CreateBlob()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        await gateway.FetchBlob("group", "project", HelloWorldSha);

        // Act
        await gateway.CreateBlob("group", "project", HelloWorldSha);

        // Assert
        gateway.IsKnownBy<IBlob>(HelloWorldSha, "group", "project").Should().BeTrue();
    }

    [Fact]
    public async Task CreateBranchNewFileNotExecutable()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(treeId, "group", "project", "main", "main", "chore(sync): gitlab sync");

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBranchNewFileExecutable()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);
        var subTree = gateway.CreateNewTree("bin");
        var emptyTree = gateway.CreateNewTree("empty");
        subTree.Tree.Add("100755", "hello.sh", HelloWorldSha, TreeType.Blob);
        newTree.Tree.Add("040000", "bin", await gateway.CreateTree(subTree, "group", "project"), TreeType.Tree);
        newTree.Tree.Add("040000", "empty", await gateway.CreateTree(emptyTree, "group", "project"), TreeType.Tree);
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(treeId, "group", "project", "main", "main", "chore(sync): gitlab sync");

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBranchUpdatedFileNotExecutable()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, world!")))
            .WithProjectOfFullPath("group/project2", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "readme.md", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project2", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(treeId, "group", "project", "main", "main", "chore(sync): gitlab sync");

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBranchUpdatedFileExecutable()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, world!"))
                .WithCommit("Add script", configure: c => c.WithFile("bin/hello.sh", "Hello, World!")))
            .WithProjectOfFullPath("group/project2", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Add script", configure: c => c.WithFile("bin/hello.sh", "echo 'Hello, World!'")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var sourceTree = await gateway.TreeFrom(new("group", "project2", TreeEntryTargetType.Tree, "main", "bin"), false);
        var scriptSha = sourceTree!.Item2.Tree.Single().Sha;
        await gateway.FetchBlob("group", "project2", scriptSha);

        var newTree = gateway.CreateNewTree("bin");
        newTree.Tree.Add("100755", "hello.sh", scriptSha, TreeType.Blob);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(treeId, "group", "project", "main", "main", "chore(sync): gitlab sync");

        // Act
        var branch = await gateway.CreateBranch("group", "project", "branch", commitId);

        // Assert
        branch.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePullRequest()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("owner/group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("owner", "group/project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "owner", "group/project");
        var commitId = await gateway.CreateCommit(treeId, "owner", "group/project", "main", "main", "chore(sync): gitlab sync");

        _ = await gateway.CreateBranch("owner", "group/project", "branch", commitId);

        // Act
        var id = await gateway.CreatePullRequest("owner", "group/project", "branch", "main", false, "GitLabSync", null);

        // Assert
        id.Should().NotBe(0);
    }

    [Fact]
    public async Task ApplyLabels()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user", isDefault: true)
            .WithProjectOfFullPath("group/project", addDefaultUserAsMaintainer: true, configure: p => p
                .WithCommit("Initial commit", configure: c => c.WithFile("readme.md", "Hello, World!")))
            .BuildServer();
        using var gateway = new GitLabGateway(server.CreateClient(), this.writeLine);

        var newTree = gateway.CreateNewTree(null);
        newTree.Tree.Add("100644", "hello.txt", HelloWorldSha, TreeType.Blob);
        await gateway.FetchBlob("group", "project", HelloWorldSha);

        var treeId = await gateway.CreateTree(newTree, "group", "project");
        var commitId = await gateway.CreateCommit(treeId, "group", "project", "main", "main", "chore(sync): gitlab sync");

        _ = await gateway.CreateBranch("group", "project", "branch", commitId);

        var id = await gateway.CreatePullRequest("group", "project", "branch", "main", false, "GitLabSync", null);

        // Act
        var labels = await gateway.ApplyLabels("group", "project", id, ["label"]);

        // Assert
        labels.Should().NotBeEmpty();
    }
}
