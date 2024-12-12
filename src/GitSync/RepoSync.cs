using System.Globalization;
using System.Text;
using GitSync.GitProvider;

namespace GitSync;

public class RepoSync(
    Action<string>? log = null,
    List<string>? labelsToApplyOnPullRequests = null,
    SyncMode? syncMode = SyncMode.IncludeAllByDefault,
    ICredentials? defaultCredentials = null,
    bool skipCollaboratorCheck = false)
{
    readonly Action<string> log = log ?? Console.WriteLine;
    readonly List<ManualSyncItem> manualSyncItems = [];
    readonly List<RepositoryInfo> sources = [];
    readonly List<RepositoryInfo> targets = [];

    public void AddBlob(string path, string? target = null) =>
        this.AddSourceItem(TreeEntryTargetType.Blob, path, target);

    public void RemoveBlob(string path, string? target = null) =>
        this.RemoveSourceItem(TreeEntryTargetType.Blob, path, target);

    public void AddSourceItem(TreeEntryTargetType type, string path, string? target = null) =>
        this.AddOrRemoveSourceItem(true, type, path, target);

    public void RemoveSourceItem(TreeEntryTargetType type, string path, string? target = null) =>
        this.AddOrRemoveSourceItem(false, type, path, target);

    public void AddOrRemoveSourceItem(bool toBeAdded, TreeEntryTargetType type, string path, string? target)
    {
        if (target == null)
        {
            this.AddOrRemoveSourceItem(toBeAdded, type, path, (ResolveTarget?)null);
            return;
        }

        this.AddOrRemoveSourceItem(toBeAdded, type, path, (_, _, _, _) => target);
    }

    public void AddOrRemoveSourceItem(bool toBeAdded, TreeEntryTargetType type, string path, ResolveTarget? target)
    {
        Guard.AgainstNullAndEmpty(path);
        //todo
        //Guard.AgainstEmpty(target, nameof(target));

        if (!toBeAdded && type == TreeEntryTargetType.Tree)
        {
            throw new NotSupportedException($"Removing a '{nameof(TreeEntryTargetType.Tree)}' isn't supported.");
        }

        if (toBeAdded && syncMode == SyncMode.IncludeAllByDefault)
        {
            throw new NotSupportedException($"Adding items is not supported when mode is '{syncMode}'");
        }

        if (!toBeAdded && syncMode == SyncMode.ExcludeAllByDefault)
        {
            throw new NotSupportedException($"Adding items is not supported when mode is '{syncMode}'");
        }

        this.manualSyncItems.Add(new(path, target));
    }

    public void AddSourceRepository(RepositoryInfo sourceRepository) =>
        this.sources.Add(sourceRepository);

    public void AddSourceRepository(string owner, string repository, string branch, ICredentials? credentials = null) =>
        this.sources.Add(new(this.OrDefaultCredentials(credentials), owner, repository, branch));

    public void AddTargetRepository(RepositoryInfo targetRepository) =>
        this.targets.Add(targetRepository);

    public void AddTargetRepository(string owner, string repository, string branch, ICredentials? credentials = null) =>
        this.targets.Add(new(this.OrDefaultCredentials(credentials), owner, repository, branch));

    ICredentials OrDefaultCredentials(ICredentials? credentials) =>
        credentials ?? defaultCredentials ?? throw new GitSyncException("defaultCredentials required");

    public async Task<SyncContext> CalculateSyncContext(RepositoryInfo targetRepository)
    {
        using var syncer = new Syncer(targetRepository.Credentials, null, this.log);
        var diffs = new List<Mapper>();
        var includedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var descriptionBuilder = new StringBuilder();
        descriptionBuilder.AppendLine("This is an automated synchronization PR.");
        descriptionBuilder.AppendLine();
        descriptionBuilder.AppendLine("The following source template repositories were used:");

        // Note: iterate backwards, later registered sources should override earlier registrations
        for (var i = this.sources.Count - 1; i >= 0; i--)
        {
            var source = this.sources[i];
            var displayName = $"{source.Owner}/{source.Repository}";
            var itemsToSync = new List<SyncItem>();

            using var gateway = source.Credentials.CreateGateway(null, this.log);
            foreach (var item in await GitProviderGatewayExtensions.GetRecursive(gateway, source.Owner, source.Repository, null, source.Branch))
            {
                if (includedPaths.Contains(item))
                {
                    continue;
                }

                includedPaths.Add(item);

                this.ProcessItem(item, itemsToSync, source);
            }

            var targetRepositoryToSync = new RepoToSync(targetRepository.Owner, targetRepository.Repository, targetRepository.Branch);

            var sourceMapper = targetRepositoryToSync.GetMapper(itemsToSync);
            var diff = await syncer.Diff(sourceMapper);
            if (diff.ToBeAddedOrUpdatedEntries.Any() ||
                diff.ToBeRemovedEntries.Any())
            {
                diffs.Add(diff);

                descriptionBuilder.AppendLine(CultureInfo.CurrentCulture, $"* {displayName}");
            }
        }

        var finalDiff = new Mapper();

        foreach (var diff in diffs)
        {
            foreach (var item in diff.ToBeAddedOrUpdatedEntries)
            {
                foreach (var value in item.Value)
                {
                    this.log($"Mapping '{item.Key.Url}' => '{value.Url}'");

                    finalDiff.Add(item.Key, value);
                }
            }

            // Note: how to deal with items to be removed
        }

        return new(targetRepository, descriptionBuilder.ToString(), finalDiff);
    }

    void ProcessItem(string item, List<SyncItem> itemsToSync, RepositoryInfo source)
    {
        var parts = new Parts(
            source.Owner,
            source.Repository,
            TreeEntryTargetType.Blob,
            source.Branch,
            item);
        var localManualSyncItems = this.manualSyncItems.Where(_ => item == _.Path).ToList();
        if (localManualSyncItems.Count != 0)
        {
            itemsToSync.AddRange(localManualSyncItems.Select(_ => new SyncItem(parts, syncMode == SyncMode.ExcludeAllByDefault, _.Target)));

            return;
        }

        itemsToSync.Add(new(parts, syncMode == SyncMode.IncludeAllByDefault, null));
    }

    public async Task<IReadOnlyList<UpdateResult>> Sync(string pullRequestTitle, string branchName, string commitMessage, SyncOutput syncOutput = SyncOutput.CreatePullRequest)
    {
        var list = new List<UpdateResult>();
        foreach (var targetRepository in this.targets)
        {
            try
            {
                var targetRepositoryDisplayName = $"{targetRepository.Owner}/{targetRepository.Repository}";

                using var syncer = new Syncer(targetRepository.Credentials, null, this.log);
                if (!await syncer.CanSynchronize(targetRepository, syncOutput, pullRequestTitle))
                {
                    continue;
                }

                var syncContext = await this.CalculateSyncContext(targetRepository);

                if (!syncContext.Diff.ToBeAddedOrUpdatedEntries.Any())
                {
                    this.log($"Repo {targetRepositoryDisplayName} is in sync");
                    continue;
                }

                var sync = await syncer.Sync(syncContext.Diff, syncOutput, branchName, commitMessage, pullRequestTitle, labelsToApplyOnPullRequests, syncContext.Description, skipCollaboratorCheck);
                if (sync.Count == 0)
                {
                    this.log($"Repo {targetRepositoryDisplayName} is in sync");
                    continue;
                }

                var createdSyncBranch = sync[0];
                this.log($"Pull created for {targetRepositoryDisplayName}, click here to review and pull: {createdSyncBranch}");
                list.Add(createdSyncBranch);
            }
            catch (Exception exception)
            {
                throw new GitSyncException($"Failed to sync Repository:{targetRepository.Repository} Branch:{targetRepository.Branch}", exception);
            }
        }

        return list;
    }
}
