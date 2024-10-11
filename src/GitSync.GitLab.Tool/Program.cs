using System.Diagnostics;
using GitSync.GitLab;
using GitSync.GitLab.Tool;
using GitSync.GitLab.Tool.Config;

var gitlabToken = Environment.GetEnvironmentVariable("GitLab_OAuthToken");
var gitlabHostUrl = Environment.GetEnvironmentVariable("GitLab_HostUrl");

if (string.IsNullOrWhiteSpace(gitlabToken))
{
    Console.WriteLine("No environment variable 'Octokit_OAuthToken' found");
    return 1;
}

var credentials = new Credentials(gitlabHostUrl ?? "https://gitlab.com", gitlabToken);

if (args.Length == 1)
{
    var path = Path.GetFullPath(args[0]);
    if (!File.Exists(path))
    {
        Console.WriteLine("Path does not exist: {0}", path);
        return 1;
    }

    return await SynchronizeRepositoriesAsync(path, credentials).ConfigureAwait(false);
}

return await SynchronizeRepositoriesAsync("githubsync.yaml", credentials).ConfigureAwait(false);

static async Task<int> SynchronizeRepositoriesAsync(string fileName, ICredentials credentials)
{
    var context = ContextLoader.Load(fileName);

    var returnValue = 0;
    var repositories = context.Repositories;
    for (var i = 0; i < repositories.Count; i++)
    {
        var targetRepository = repositories[i];

        var prefix = $"[({i + 1} / {repositories.Count})]";

        Console.WriteLine($"{prefix} Setting up synchronization for '{targetRepository}'");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await SyncRepository(context, targetRepository, credentials).ConfigureAwait(false);

            Console.WriteLine($"{prefix} Synchronized '{targetRepository}', took {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }
        catch (Exception exception)
        {
            returnValue = 1;
            Console.WriteLine($"Failed to synchronize '{targetRepository}'. Exception: {exception}");

            Console.WriteLine("Press a key to continue...");
            Console.ReadKey();
        }
    }

    return returnValue;
}

static Task<IReadOnlyList<UpdateResult>> SyncRepository(Context context, Repository targetRepository, ICredentials credentials)
{
    var sync = new RepoSync(Console.WriteLine);

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

    return sync.Sync(syncOutput);
}

static RepositoryInfo BuildInfo(string url, string branch, ICredentials credentials)
{
    var company = UrlHelper.GetCompany(url);
    var project = UrlHelper.GetProject(url);
    return new RepositoryInfo(credentials, company, project, branch);
}
