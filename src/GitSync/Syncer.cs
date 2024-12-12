using System.Diagnostics;
using System.Globalization;
using System.Net;
using GitSync.GitProvider;
using ICredentials = GitSync.GitProvider.ICredentials;

namespace GitSync;

sealed class Syncer :
    IDisposable
{
    readonly IGitProviderGateway gateway;
    readonly Action<string> log;
    readonly ICredentials credentials;

    public Syncer(
        ICredentials credentials,
        IWebProxy? proxy = null,
        Action<string>? log = null)
    {
        this.log = log ?? nullLogger;
        this.credentials = credentials;
        this.gateway = this.credentials.CreateGateway(proxy, this.log);
    }

    static readonly Action<string> nullLogger = _ => { };

    internal async Task<Mapper> Diff(Mapper input)
    {
        Guard.AgainstNull(input);
        var outMapper = new Mapper();

        foreach (var kvp in input.ToBeAddedOrUpdatedEntries)
        {
            var source = kvp.Key;

            this.log($"Diff - Analyze {source.Type} source '{source.Url}'.");

            var richSource = await this.EnrichWithShas(source, true);

            foreach (var destination in kvp.Value)
            {
                this.log($"Diff - Analyze {source.Type} target '{destination.Url}'.");

                var richDestination = await this.EnrichWithShas(destination, false);

                var sourceSha = richSource.Sha!;
                if (sourceSha == richDestination.Sha &&
                    richSource.Mode == richDestination.Mode)
                {
                    this.log($"Diff - No sync required. Matching sha ({sourceSha[..7]}) between target '{destination.Url}' and source '{source.Url}.");

                    continue;
                }

                this.log(string.Format(CultureInfo.CurrentCulture, "Diff - {4} required. Non-matching sha ({0} vs {1}) between target '{2}' and source '{3}.",
                    sourceSha[..7], richDestination.Sha?[..7] ?? "NULL", destination.Url, source.Url, richDestination.Sha == null ? "Creation" : "Updation"));

                outMapper.Add(richSource, richDestination);
            }
        }

        foreach (var p in input.ToBeRemovedEntries)
        {
            outMapper.Remove(p);
        }

        return outMapper;
    }

    internal async Task<bool> CanSynchronize(RepositoryInfo targetRepository, SyncOutput expectedOutput, string pullRequestTitle)
    {
        if (expectedOutput is SyncOutput.CreatePullRequest or SyncOutput.MergePullRequest)
        {
            var hasOpenPullRequests = await this.gateway.HasOpenPullRequests(targetRepository.Owner, targetRepository.Repository, pullRequestTitle);
            if (hasOpenPullRequests)
            {
                this.log("Cannot create pull request, there is an existing open pull request, close or merge that first");
                return false;
            }
        }

        return true;
    }

    internal async Task<IReadOnlyList<UpdateResult>> Sync(
        Mapper diff,
        SyncOutput expectedOutput,
        string branchName,
        string commitMessage,
        string pullRequestTitle,
        IEnumerable<string>? labelsToApplyOnPullRequests = null,
        string? description = null,
        bool skipCollaboratorCheck = false)
    {
        Guard.AgainstNull(diff);
        Guard.AgainstNull(expectedOutput);
        var labels = labelsToApplyOnPullRequests?.ToArray() ?? [];

        if (labels.Length != 0 &&
            expectedOutput != SyncOutput.CreatePullRequest)
        {
            throw new GitSyncException($"Labels can only be applied in '{SyncOutput.CreatePullRequest}' mode.");
        }

        var t = diff.Transpose();

        var results = new List<UpdateResult>();

        foreach (var updatesPerOwnerRepositoryBranch in t.Values)
        {
            var updates = await this.ProcessUpdates(expectedOutput, updatesPerOwnerRepositoryBranch, labels, description, skipCollaboratorCheck, branchName, pullRequestTitle, commitMessage);
            results.Add(updates);
        }

        return results;
    }

    async Task<UpdateResult> ProcessUpdates(
        SyncOutput expectedOutput,
        IList<Tuple<Parts, IParts>> updatesPerOwnerRepositoryBranch,
        string[] labels,
        string? description,
        bool skipCollaboratorCheck,
        string branchName,
        string pullRequestTitle,
        string commitMessage)
    {
        var root = updatesPerOwnerRepositoryBranch.First().Item1.Root();

        string commitSha;

        var isCollaborator = skipCollaboratorCheck ||
                             await this.gateway.IsCollaborator(root.Owner, root.Repository);
        if (isCollaborator)
        {
            commitSha = await this.ProcessUpdatesInTargetRepository(root, updatesPerOwnerRepositoryBranch, commitMessage);
        }
        else
        {
            this.log("User is not a collaborator, need to create a fork");

            if (expectedOutput != SyncOutput.CreatePullRequest)
            {
                throw new NotSupportedException($"User is not a collaborator, sync output '{expectedOutput}' is not supported, only creating PRs is supported");
            }

            commitSha = await this.ProcessUpdatesInFork(root, branchName, updatesPerOwnerRepositoryBranch, commitMessage);
        }

        if (expectedOutput == SyncOutput.CreateCommit)
        {
            return new($"https://github.com/{root.Owner}/{root.Repository}/commit/{commitSha}", commitSha, null, null);
        }

        if (expectedOutput == SyncOutput.CreateBranch)
        {
            branchName = await this.gateway.CreateBranch(root.Owner, root.Repository, branchName, commitSha);
            return new($"https://github.com/{root.Owner}/{root.Repository}/compare/{UrlSanitize(root.Branch)}...{UrlSanitize(branchName)}", commitSha, branchName, null);
        }

        if (expectedOutput is SyncOutput.CreatePullRequest or SyncOutput.MergePullRequest)
        {
            var merge = expectedOutput == SyncOutput.MergePullRequest;
            var prSourceBranch = branchName;

            if (isCollaborator)
            {
                await this.gateway.CreateBranch(root.Owner, root.Repository, branchName, commitSha);
            }
            else
            {
                // Never auto-merge
                merge = false;

                var forkedRepository = await this.gateway.Fork(root.Owner, root.Repository);
                prSourceBranch = $"{forkedRepository.Owner.Login}:{prSourceBranch}";
            }

            var prNumber = await this.gateway.CreatePullRequest(root.Owner, root.Repository, prSourceBranch, root.Branch, merge, pullRequestTitle, description);

            if (isCollaborator)
            {
                await this.gateway.ApplyLabels(root.Owner, root.Repository, prNumber, labels);
            }

            return new($"https://github.com/{root.Owner}/{root.Repository}/pull/{prNumber}", commitSha, root.Branch, prNumber);
        }

        throw new NotSupportedException();
    }

    async Task<string> ProcessUpdatesInTargetRepository(Parts root, IList<Tuple<Parts, IParts>> updatesPerOwnerRepositoryBranch, string commitMessage)
    {
        var tt = new TargetTree(root);

        foreach (var change in updatesPerOwnerRepositoryBranch)
        {
            var source = change.Item2;
            var destination = change.Item1;

            switch (source)
            {
                case Parts toAddOrUpdate:
                    tt.Add(destination, toAddOrUpdate);
                    break;

                case Parts.NullParts:
                    tt.Remove(destination);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported 'from' type ({source.GetType().FullName}).");
            }
        }

        var btt = await this.BuildTargetTree(tt);

        var parentCommit = await this.gateway.RootCommitFrom(root);

        var commitSha = await this.gateway.CreateCommit(btt, root.Owner, root.Repository, parentCommit.Sha, root.Branch, commitMessage);
        return commitSha;
    }

    async Task<string> ProcessUpdatesInFork(Parts root, string temporaryBranchName, IList<Tuple<Parts, IParts>> updatesPerOwnerRepositoryBranch, string commitMessage)
    {
        var forkedRepository = await this.gateway.Fork(root.Owner, root.Repository);

        var temporaryPath = Path.Combine(Path.GetTempPath(), "GitHubSync", root.Owner, root.Repository);

        if (Directory.Exists(temporaryPath))
        {
            Directory.Delete(temporaryPath, true);
        }

        Directory.CreateDirectory(temporaryPath);

        // Step 1: clone the fork
        var repositoryPath = LibGit2Sharp.Repository.Clone(forkedRepository.CloneUrl, temporaryPath, new()
        {
            BranchName = root.Branch
        });

        var currentUser = await this.gateway.GetCurrentUser();
        var commitSignature = new LibGit2Sharp.Signature(currentUser.Name, currentUser.Email ?? "hidden@protected.com", DateTimeOffset.Now);

        using var repository = new LibGit2Sharp.Repository(repositoryPath);
        // Step 2: ensure upstream
        var remotes = repository.Network.Remotes;
        var originRemote = remotes["origin"];
        var upstreamRemote = remotes["upstream"] ?? remotes.Add("upstream", $"https://github.com/{root.Owner}/{root.Repository}");

        LibGit2Sharp.Commands.Fetch(repository, "upstream", upstreamRemote.FetchRefSpecs.Select(_ => _.Specification), null, null);

        // Step 3: create local branch
        var tempBranch = repository.Branches.Add(temporaryBranchName, "HEAD");
        repository.Branches.Update(tempBranch, b =>
        {
            b.Remote = originRemote.Name;
            b.UpstreamBranch = tempBranch.CanonicalName;
            //b.Upstream = $"refs/heads/{temporaryBranchName}";
            //b.UpstreamBranch = $""
        });

        LibGit2Sharp.Commands.Checkout(repository, tempBranch);

        // Step 4: ensure we have the latest
        var upstreamMasterBranch = repository.Branches["upstream/master"];

        repository.Merge(upstreamMasterBranch, commitSignature, new());

        // Step 5: create delta
        foreach (var change in updatesPerOwnerRepositoryBranch)
        {
            var source = change.Item2;
            var destination = change.Item1;
            var fullDestination = Path.Combine(temporaryPath, destination.Path!.Replace('/', Path.DirectorySeparatorChar));

            switch (source)
            {
                case Parts parts:
                    // Directly download raw bytes into file
                    await using (var fileStream = new FileStream(fullDestination, FileMode.Create))
                    {
                        await this.gateway.DownloadBlob(parts, fileStream);
                    }
                    break;

                case Parts.NullParts:
                    File.Delete(fullDestination);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported 'from' type ({source.GetType().FullName}).");
            }
        }

        // Step 6: stage all files
        LibGit2Sharp.Commands.Stage(repository, "*");

        // Step 7: create & push commit
        var commit = repository.Commit(commitMessage, commitSignature, commitSignature,
            new());

        repository.Network.Push(tempBranch, new()
        {
            CredentialsProvider = (_, _, _) => this.credentials.CreateLibGit2SharpCredentials()
        });

        return commit.Sha;
    }

    static string UrlSanitize(string branch) =>
        branch.Replace("/", ";");

    async Task<string> BuildTargetTree(TargetTree tt)
    {
        var treeFrom = await this.gateway.TreeFrom(tt.Current, false);

        INewTree newTree;
        if (treeFrom == null)
        {
            newTree = this.gateway.CreateNewTree(tt.Current.Path);
        }
        else
        {
            var destinationParentTree = treeFrom.Item2;
            newTree = this.BuildNewTreeFrom(destinationParentTree);
        }

        foreach (var st in tt.SubTreesToUpdate.Values)
        {
            RemoveTreeItemFrom(newTree, st.Current.Name!);
            var sha = await this.BuildTargetTree(st);

            if (string.Equals(sha, TargetTree.EmptyTreeSha, StringComparison.OrdinalIgnoreCase))
            {
                // Resulting tree contains no items
                continue;
            }

            newTree.Tree.Add(
                "040000",
                st.Current.Name!,
                sha,
                TreeType.Tree);
        }

        foreach (var l in tt.LeavesToDrop.Values)
        {
            RemoveTreeItemFrom(newTree, l.Name!);
        }

        foreach (var l in tt.LeavesToCreate.Values)
        {
            var destination = l.Item1;
            var source = l.Item2;

            RemoveTreeItemFrom(newTree, destination.Name!);

            await this.SyncLeaf(source, destination);

            switch (source.Type)
            {
                case TreeEntryTargetType.Blob:
                    var blobFrom = await this.gateway.BlobFrom(source, true);
                    if (blobFrom == null)
                    {
                        continue;
                    }
                    var sourceBlobItem = blobFrom.Item2;
                    newTree.Tree.Add(
                        sourceBlobItem.Mode,
                        destination.Name!,
                        source.Sha!,
                        TreeType.Blob);
                    break;

                case TreeEntryTargetType.Tree:
                    newTree.Tree.Add(
                        "040000",
                        destination.Name!,
                        source.Sha!,
                        TreeType.Tree);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        if (newTree.Tree.Count == 0)
        {
            return TargetTree.EmptyTreeSha;
        }

        return await this.gateway.CreateTree(newTree, tt.Current.Owner, tt.Current.Repository);
    }

    Task SyncLeaf(Parts source, Parts destination)
    {
        var sourceSha = source.Sha!;
        var shortSha = sourceSha[..7];
        switch (source.Type)
        {
            case TreeEntryTargetType.Blob:
                this.log($"Sync - Determine if Blob '{shortSha}' requires to be created in '{destination.Owner}/{destination.Repository}'.");
                return this.SyncBlob(source.Owner, source.Repository, sourceSha, destination.Owner, destination.Repository);
            case TreeEntryTargetType.Tree:
                this.log($"Sync - Determine if Tree '{shortSha}' requires to be created in '{destination.Owner}/{destination.Repository}'.");
                return this.SyncTree(source, destination.Owner, destination.Repository);
            default:
                throw new NotSupportedException();
        }
    }

    static void RemoveTreeItemFrom(INewTree tree, string name)
    {
        var existing = tree.Tree.SingleOrDefault(ti => ti.Path == name);

        if (existing == null)
        {
            return;
        }

        tree.Tree.Remove(existing);
    }

    INewTree BuildNewTreeFrom(ITreeResponse destinationParentTree)
    {
        var newTree = this.gateway.CreateNewTree(destinationParentTree.Path);

        foreach (var treeItem in destinationParentTree.Tree)
        {
            newTree.Tree.Add(
                treeItem.Mode,
                treeItem.Name,
                treeItem.Sha,
                treeItem.Type);
        }

        return newTree;
    }

    async Task SyncBlob(string sourceOwner, string sourceRepository, string sha, string destinationOwner, string destinationRepository)
    {
        if (this.gateway.IsKnownBy<IBlob>(sha, destinationOwner, destinationRepository))
        {
            return;
        }

        await this.gateway.FetchBlob(sourceOwner, sourceRepository, sha);
        await this.gateway.CreateBlob(destinationOwner, destinationRepository, sha);
    }

    async Task SyncTree(Parts source, string destinationOwner, string destinationRepository)
    {
        var sourceSha = source.Sha!;

        if (this.gateway.IsKnownBy<ITreeResponse>(sourceSha, destinationOwner, destinationRepository))
        {
            return;
        }

        var treeFrom = await this.gateway.TreeFrom(source, true);

        if (treeFrom == null)
        {
            return;
        }

        var newTree = this.gateway.CreateNewTree(source.Path ?? string.Empty);

        foreach (var i in treeFrom.Item2.Tree)
        {
            var value = i.Type;
            switch (value)
            {
                case TreeType.Blob:
                    await this.SyncBlob(source.Owner, source.Repository, i.Sha, destinationOwner, destinationRepository);
                    break;

                case TreeType.Tree:
                    await this.SyncTree(treeFrom.Item1.Combine(TreeEntryTargetType.Tree, i.Path, i.Sha, i.Mode), destinationOwner, destinationRepository);
                    break;
                case TreeType.Commit:
                    break;
                default:
                    throw new NotSupportedException();
            }

            newTree.Tree.Add(i.Mode, i.Name, i.Sha, value);
        }

        // ReSharper disable once RedundantAssignment
        var sha = await this.gateway.CreateTree(newTree, destinationOwner, destinationRepository);

        Debug.Assert(sourceSha == sha);
    }

    async Task<Parts> EnrichWithShas(Parts part, bool throwsIfNotFound)
    {
        var outPart = part;

        switch (part.Type)
        {
            case TreeEntryTargetType.Tree:
                var t = await this.gateway.TreeFrom(part, throwsIfNotFound);
                if (t != null)
                {
                    outPart = t.Item1;
                }

                break;

            case TreeEntryTargetType.Blob:
                var b = await this.gateway.BlobFrom(part, throwsIfNotFound);
                if (b != null)
                {
                    outPart = b.Item1;
                }

                break;

            default:
                throw new NotSupportedException();
        }

        return outPart;
    }

    public void Dispose() =>
        this.gateway.Dispose();
}
