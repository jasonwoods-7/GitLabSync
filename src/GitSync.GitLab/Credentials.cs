using System.Net;
using GitSync.GitProvider;
using NGitLab;
using ICredentials = GitSync.GitProvider.ICredentials;

namespace GitSync.GitLab;

public class Credentials(string hostUrl, string token) : ICredentials
{
    public IGitProviderGateway CreateGateway(IWebProxy? webProxy, Action<string> log) =>
        new GitLabGateway(new GitLabClient(hostUrl, token), log);

    public LibGit2Sharp.Credentials CreateLibGit2SharpCredentials() =>
        new LibGit2Sharp.UsernamePasswordCredentials
        {
            Username = token,
            Password = string.Empty
        };
}
