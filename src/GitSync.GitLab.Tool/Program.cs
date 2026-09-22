using System.CommandLine;
using System.Diagnostics;
using GitSync;
using GitSync.GitLab;
using GitSync.GitLab.Tool;
using GitSync.GitLab.Tool.Config;
using GitSync.GitProvider;
using Microsoft.Extensions.Logging;

const string DefaultConfigFileName = "gitlabsync.yaml";

var rootCommand = new RootCommand(
    "A tool to keep files in a source branch in sync with a target branch"
);

var gitlabTokenOption = new Option<string>("--token", "-t")
{
    Description =
        "GitLab access token. Prefer the GitLab_OAuthToken environment variable; use this only for ad-hoc runs, since it can be visible in shell history and process listings.",
};
var gitlabHostUrlOption = new Option<string>("--host-url", "-u")
{
    Description = "GitLab instance base URL (defaults to https://gitlab.com)",
};
var filenameArgument = new Argument<string>("filename")
{
    Description = "Path to the GitLabSync YAML configuration file",
    DefaultValueFactory = _ => DefaultConfigFileName,
};

rootCommand.Add(gitlabTokenOption);
rootCommand.Add(gitlabHostUrlOption);
rootCommand.Add(filenameArgument);

rootCommand.SetAction(async parseResult =>
{
    var gitlabToken = ResolveValue(parseResult.GetValue(gitlabTokenOption), "GitLab_OAuthToken");
    var gitlabHostUrl = ResolveValue(parseResult.GetValue(gitlabHostUrlOption), "GitLab_HostUrl");
    var filenameArgumentValue = parseResult.GetValue(filenameArgument);

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

    if (string.IsNullOrWhiteSpace(filenameArgumentValue))
    {
        logger.PathDoesNotExist(filenameArgumentValue ?? string.Empty);
        return 1;
    }

    var credentials = new Credentials(gitlabHostUrl ?? "https://gitlab.com", gitlabToken);

    var filename = Path.GetFullPath(filenameArgumentValue);
    if (!File.Exists(filename))
    {
        logger.PathDoesNotExist(filename);
        return 1;
    }

    return await SynchronizeRepositoriesAsync(filename, credentials, logger).ConfigureAwait(false);
});

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);

static async Task<int> SynchronizeRepositoriesAsync(
    string fileName,
    ICredentials credentials,
    ILogger logger
)
{
    var context = ContextLoader.Load(fileName);

    var returnValue = 0;
    var repositories = context.Repositories;
    for (var i = 0; i < repositories.Count; i++)
    {
        var targetRepository = repositories[i];

        var prefix = $"[({i + 1} / {repositories.Count})]";
        using var _ = logger.BeginScope(prefix);

        logger.SettingUpSynchronization(targetRepository);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await SyncRepository(context, targetRepository, credentials, logger)
                .ConfigureAwait(false);

            logger.Synchronized(targetRepository, stopwatch.Elapsed);
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
    ILogger logger
)
{
    var sync = new RepoSync(logger, targetRepository.Labels);

    var targetInfo = BuildInfo(
        targetRepository.Url,
        targetRepository.Branch,
        new HashSet<string>(StringComparer.Ordinal),
        credentials
    );
    sync.AddTargetRepository(targetInfo);

    foreach (
        var sourceRepository in targetRepository.Templates.Select(t =>
            context.Templates.First(x => x.name == t)
        )
    )
    {
        var sourceInfo = BuildInfo(
            sourceRepository.url,
            sourceRepository.branch,
            new HashSet<string>(sourceRepository.ignore, StringComparer.Ordinal),
            credentials
        );
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
        syncOutput
    );
}

static RepositoryInfo BuildInfo(
    string url,
    string branch,
    IReadOnlySet<string> ignorePaths,
    ICredentials credentials
)
{
    var company = UrlHelper.GetCompany(url);
    var project = UrlHelper.GetProject(url);
    return new RepositoryInfo(credentials, company, project, branch, ignorePaths);
}

static string? ResolveValue(string? cliValue, string environmentVariableName) =>
    string.IsNullOrWhiteSpace(cliValue)
        ? Environment.GetEnvironmentVariable(environmentVariableName)
        : cliValue;
