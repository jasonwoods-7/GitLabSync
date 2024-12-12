using System.Net;

namespace GitSync.GitProvider;

public interface ICredentials
{
    IGitProviderGateway CreateGateway(IWebProxy? webProxy, Action<string> log);

    LibGit2Sharp.Credentials CreateLibGit2SharpCredentials();
}
