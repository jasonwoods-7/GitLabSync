using System.Diagnostics;
using System.Globalization;
using System.Net;
using NGitLab;
using NGitLab.Models;

namespace GitSync.GitLab;

sealed class GitLabGateway(IGitLabClient client, Action<string> log) : IGitProviderGateway
{
    const string ExecutableMode = "100755";

    readonly Dictionary<string, Tuple<Parts, ITreeItem>?> blobCachePerPath = [];
    readonly Dictionary<string, Tuple<Parts, ITreeResponse>?> treeCachePerPath = [];
    readonly Dictionary<string, Commit> commitCachePerOwnerRepositoryBranch = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, INewTree> treeCache = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (string, string, string)> commitCache = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, ISet<string>> knownBlobsPerRepository = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, ISet<string>> knownTreesPerRepository = new(StringComparer.OrdinalIgnoreCase);

    readonly Lazy<string> blobStoragePath = new(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"GitHubSync-{Guid.NewGuid()}");
        Directory.CreateDirectory(path);
        log($"Ctor - Create temp blob storage '{path}'.");
        return path;
    });

    public void Dispose()
    {
        if (this.blobStoragePath.IsValueCreated)
        {
            Directory.Delete(this.blobStoragePath.Value, true);
            log($"Dispose - Delete temp blob storage '{this.blobStoragePath.Value}'.");
        }
    }

    public Task<IUser> GetCurrentUser() =>
        Task.FromResult<IUser>(new User(client.Users.Current));

    public async Task<bool> IsCollaborator(string owner, string name)
    {
        var project = await client
            .Projects
            .GetByIdAsync(await client.GetProjectId(owner, name), new());

        return project.Permissions.ProjectAccess is { AccessLevel: >= AccessLevel.Developer } ||
               project.Permissions.GroupAccess is { AccessLevel: >= AccessLevel.Developer };
    }

    public async Task<IRepository> Fork(string owner, string name)
    {
        var forked = await client
            .Projects
            .ForkAsync((await client.GetProjectId(owner, name)).ToString(CultureInfo.InvariantCulture), new());

        var project = await client.Projects.GetByIdAsync(forked.Id, new());

        while (project.ImportStatus != "finished")
        {
            await Task.Delay(1000);
            project = await client.Projects.GetByIdAsync(forked.Id, new());
        }

        return new Repository(project);
    }

    public async Task DownloadBlob(Parts source, Stream targetStream) =>
        client
            .GetRepository(await client.GetProjectId(source.Owner, source.Repository))
            .GetRawBlob(source.Sha!, p => p.CopyTo(targetStream));

    public async Task<bool> HasOpenPullRequests(string owner, string name, string prTitle) =>
        client
            .GetMergeRequest(await client.GetProjectId(owner, name))
            .AllInState(MergeRequestState.opened)
            .Any(mr => mr.Title == prTitle);

    public async Task<ICommit> RootCommitFrom(Parts source)
    {
        var orb = $"{source.Owner}/{source.Repository}/{source.Branch}";
        if (this.commitCachePerOwnerRepositoryBranch.TryGetValue(orb, out var commit))
        {
            return commit;
        }

        var repositoryClient = client
            .GetRepository(await client.GetProjectId(source.Owner, source.Repository));

        commit = await Commit.CreateAsync(
            repositoryClient.Branches[source.Branch].Commit,
            repositoryClient);

        this.commitCachePerOwnerRepositoryBranch[orb] = commit;

        return commit;
    }

    public async Task<Tuple<Parts, ITreeResponse>?> TreeFrom(Parts source, bool throwsIfNotFound)
    {
        if (this.treeCachePerPath.TryGetValue(source.Url, out var result))
        {
            return result;
        }

        try
        {
            var tree = client
                .GetRepository(await client.GetProjectId(source.Owner, source.Repository))
                .GetTreeAsync(new() { Ref = source.Branch, Path = source.Path });

            var treeResponse = new TreeResponse(source.Path ?? "", tree);
            result = new(source, treeResponse);
            this.treeCachePerPath.Add(source.Url, result);
            return result;
        }
        catch (GitLabException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            if (throwsIfNotFound)
            {
                throw new GitSyncException($"[{source.Type}: {source.Url}] doesn't exist.", ex);
            }

            return null;
        }
    }

    public async Task<Tuple<Parts, ITreeItem>?> BlobFrom(Parts source, bool throwsIfNotFound)
    {
        if (this.blobCachePerPath.TryGetValue(source.Url, out var result))
        {
            return result;
        }

        var parentPathIndex = source.Path?.LastIndexOf('/') ?? -1;
        var parentPath = parentPathIndex == -1 ? null : source.Path![..parentPathIndex];

        NGitLab.Models.Tree? blob = null;

        try
        {
            blob = client
                .GetRepository(await client.GetProjectId(source.Owner, source.Repository))
                .GetTreeAsync(new() { Ref = source.Branch, Path = parentPath })
                .FirstOrDefault(t => t.Name == source.Name);
        }
        catch (GitLabException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }

        if (blob is null)
        {
            if (throwsIfNotFound)
            {
                throw new GitSyncException($"[{source.Type}: {source.Url}] doesn't exist.");
            }

            return null;
        }

        var parts = new Parts(source.Owner, source.Repository, TreeEntryTargetType.Blob, source.Branch, source.Path, blob.Id.ToString(), blob.Mode);
        var item = new TreeItem(blob);

        result = new(parts, item);
        this.blobCachePerPath[source.Url] = result;
        return result;
    }

    void AddToKnown<T>(string sha, string owner, string repository)
    {
        var dic = this.CacheFor<T>();

        var or = $"{owner}/{repository}";

        if (!dic.TryGetValue(sha, out var l))
        {
            l = new HashSet<string>();
            dic.Add(sha, l);
        }

        l.Add(or);
    }

    public bool IsKnownBy<T>(string sha, string owner, string repository)
    {
        var dic = this.CacheFor<T>();

        if (!dic.TryGetValue(sha, out var l))
        {
            return false;
        }

        var or = $"{owner}/{repository}";

        return l.Contains(or);
    }

    Dictionary<string, ISet<string>> CacheFor<T>()
    {
        if (typeof(T) == typeof(IBlob))
        {
            return this.knownBlobsPerRepository;
        }

        if (typeof(T) == typeof(ITreeResponse))
        {
            return this.knownTreesPerRepository;
        }

        throw new NotSupportedException();
    }

    public async Task<string> CreateCommit(string treeSha, string owner, string repo, string parentCommitSha, string branch)
    {
        var commitId = await GitHashHelper.GetCommitHash(treeSha, parentCommitSha, client.Users.Current);

        this.commitCache[commitId] = (treeSha, parentCommitSha, branch);

        return commitId;
    }

    public async Task<string> CreateTree(INewTree newTree, string owner, string repo)
    {
        var treeId = await GitHashHelper.GetTreeHash(newTree);

        this.AddToKnown<ITreeResponse>(treeId, owner, repo);
        this.treeCache[treeId] = newTree;

        return treeId;
    }

    public
#if DEBUG
        async
#endif
        Task CreateBlob(string owner, string repository, string sha)
    {
#if DEBUG
        var blobPath = Path.Combine(this.blobStoragePath.Value, sha);

        await using var blob = File.OpenRead(blobPath);

        var hash = await GitHashHelper.GetBlobHash(blob);
        Debug.Assert(string.Equals(sha, hash, StringComparison.OrdinalIgnoreCase));
#endif

        this.AddToKnown<IBlob>(sha, owner, repository);

#if !DEBUG
        return Task.CompletedTask;
#endif
    }

    public async Task FetchBlob(string owner, string repository, string sha)
    {
        var blobPath = Path.Combine(this.blobStoragePath.Value, sha);

        if (File.Exists(blobPath))
        {
            return;
        }

        log($"API Query - Retrieve blob '{sha[..7]}' details from '{owner}/{repository}'.");

        client
            .GetRepository(await client.GetProjectId(owner, repository))
            .GetRawBlob(sha, s =>
            {
                using var fs = File.Create(blobPath);
                s.CopyTo(fs);
            });
    }

    public async Task<string> CreateBranch(string owner, string repository, string branchName, string commitSha)
    {
        var (treeSha, parentCommitSha, sourceBranch) = this.commitCache[commitSha];

        var repo = client.GetRepository(await client.GetProjectId(owner, repository));
        var parentTree = repo.GetTreeAsync(new() { Ref = sourceBranch, Recursive = true }).ToList();
        var updatedTree = this.treeCache[treeSha]
            .Tree
            .SelectMany(t =>
            {
                if (t.Type == TreeType.Tree && this.treeCache.TryGetValue(t.Sha, out var subTree))
                {
                    return subTree.Tree.ToArray();
                }

                return [t];
            })
            .SelectMany(t => this.CreateCommitActionSelector(t, parentTree))
            .ToList();

        var commit = client
            .GetCommits(await client.GetProjectId(owner, repository))
            .Create(new()
            {
                CommitMessage = $"chore(sync): gitLabSync update - {branchName}",
                Branch = branchName,
                StartSha = parentCommitSha.ToLower(CultureInfo.InvariantCulture),
                Actions = updatedTree
            });

        return commit.WebUrl;
    }

    IEnumerable<CreateCommitAction> CreateCommitActionSelector(INewTreeItem item, IReadOnlyList<NGitLab.Models.Tree> parentTree)
    {
        if (item.Type != TreeType.Blob)
        {
            yield break;
        }

        var updated = parentTree.FirstOrDefault(p => p.Path == item.Path);

        if (updated is null)
        {
            yield return new()
            {
                Action = "create",
                FilePath = item.Path,
                Content = File.ReadAllText(Path.Combine(this.blobStoragePath.Value, item.Sha))
            };

            if (item.Mode == ExecutableMode)
            {
                yield return new()
                {
                    Action = "chmod",
                    FilePath = item.Path,
                    IsExecutable = true
                };
            }

            yield break;
        }

        if (item.Sha != updated.Id.ToString())
        {
            yield return new()
            {
                Action = "update",
                FilePath = item.Path,
                Content = File.ReadAllText(Path.Combine(this.blobStoragePath.Value, item.Sha))
            };
        }

        if (item.Mode != updated.Mode)
        {
            yield return new()
            {
                Action = "chmod",
                FilePath = item.Path,
                IsExecutable = item.Mode == ExecutableMode
            };
        }
    }

    public async Task<int> CreatePullRequest(string owner, string repository, string branch, string targetBranch, bool merge, string? description)
    {
        var mergeRequest = client
            .GetMergeRequest(await client.GetProjectId(owner, repository))
            .Create(new()
            {
                SourceBranch = branch,
                TargetBranch = targetBranch,
                Title = $"GitHubSync update - {targetBranch}",
                Description = description,
                RemoveSourceBranch = true
            });

        return mergeRequest.Iid;
    }

    public async Task<IReadOnlyList<ILabel>> ApplyLabels(string owner, string repository, int issueNumber, string[] labels)
    {
        if (labels.Length == 0)
        {
            return [];
        }

        return client
            .GetMergeRequest(await client.GetProjectId(owner, repository))
            .Update(issueNumber, new()
            {
                AddLabels = string.Join(",", labels)
            })
            .Labels
            .Select(l => new Label(l))
            .ToList();
    }

    public INewTree CreateNewTree(string? path) =>
        new NewTree(string.IsNullOrWhiteSpace(path) ? "" : $"{path}/");
}
