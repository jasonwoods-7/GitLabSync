using System.Diagnostics;
using System.Globalization;
using GitSync;
using GitSync.GitLab;
using GitSync.GitLab.Tool;
using GitSync.GitLab.Tool.Config;
using GitSync.GitProvider;
using Microsoft.Extensions.Logging;

var gitlabToken = Environment.GetEnvironmentVariable("GitLab_OAuthToken");
var gitlabHostUrl = Environment.GetEnvironmentVariable("GitLab_HostUrl");

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "[HH:mm:ss] ";
        })
        .SetMinimumLevel(LogLevel.Trace);
});
var logger = loggerFactory.CreateLogger<Program>();

if (string.IsNullOrWhiteSpace(gitlabToken))
{
    logger.NoGitLabToken();
    return 1;
}

var credentials = new Credentials(gitlabHostUrl ?? "https://gitlab.com", gitlabToken);

if (args.Length == 1)
{
    var path = Path.GetFullPath(args[0]);
    if (!File.Exists(path))
    {
        logger.PathDoesNotExist(path);
        return 1;
    }

    return await SynchronizeRepositoriesAsync(path, credentials, logger).ConfigureAwait(false);
}

return await SynchronizeRepositoriesAsync("gitlabsync.yaml", credentials, logger).ConfigureAwait(false);

static async Task<int> SynchronizeRepositoriesAsync(string fileName, ICredentials credentials, ILogger logger)
{
    var context = ContextLoader.Load(fileName);

    var returnValue = 0;
    var repositories = context.Repositories;
    for (var i = 0; i < repositories.Count; i++)
    {
        var targetRepository = repositories[i];

        var prefix = $"[({i + 1} / {repositories.Count})]";

        logger.SettingUpSynchronization(prefix, targetRepository);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await SyncRepository(context, targetRepository, credentials, logger).ConfigureAwait(false);

            logger.Synchronized(prefix, targetRepository, stopwatch.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture));
        }
        catch (Exception exception)
        {
            returnValue = 1;
            logger.FailedToSynchronize(exception, targetRepository);
        }
    }

    return returnValue;
}

static Task<IReadOnlyList<UpdateResult>> SyncRepository(
    Context context,
    Repository targetRepository,
    ICredentials credentials,
    ILogger logger)
{
    var sync = new RepoSync(logger, targetRepository.Labels);

    var targetInfo = BuildInfo(targetRepository.Url, targetRepository.Branch, credentials);
    sync.AddTargetRepository(targetInfo);

    foreach (var sourceRepository in targetRepository.Templates
                 .Select(t => context.Templates.First(x => x.name == t)))
    {
        var sourceInfo = BuildInfo(sourceRepository.url, sourceRepository.branch, credentials);
        sync.AddSourceRepository(sourceInfo);
    }

    var syncOutput = SyncOutput.CreatePullRequest;

    if (targetRepository.AutoMerge)
    {
        syncOutput = SyncOutput.MergePullRequest;
    }

    var branchName = $"GitLabSync-{DateTime.Now:yyyyMMdd-HHmmss}";
    return sync.Sync(
        $"GitLabSync update - {targetRepository.Branch}",
        branchName,
        $"chore(sync): gitLabSync update - {branchName}",
        syncOutput);
}

static RepositoryInfo BuildInfo(string url, string branch, ICredentials credentials)
{
    var company = UrlHelper.GetCompany(url);
    var project = UrlHelper.GetProject(url);
    return new RepositoryInfo(credentials, company, project, branch);
}
