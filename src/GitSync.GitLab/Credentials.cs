using System.Net;
using GitSync.GitProvider;
using Microsoft.Extensions.Logging;
using NGitLab;
using ICredentials = GitSync.GitProvider.ICredentials;

namespace GitSync.GitLab;

public class Credentials(string hostUrl, string token) : ICredentials
{
    public IGitProviderGateway CreateGateway(IWebProxy? webProxy, ILogger logger) =>
        new GitLabGateway(new GitLabClient(hostUrl, token), logger);

    public LibGit2Sharp.Credentials CreateLibGit2SharpCredentials() =>
        new LibGit2Sharp.UsernamePasswordCredentials
        {
            Username = token,
            Password = string.Empty
        };
}
