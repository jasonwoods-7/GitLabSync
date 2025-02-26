using System.Net;
using Microsoft.Extensions.Logging;

namespace GitSync.GitProvider;

public interface ICredentials
{
    IGitProviderGateway CreateGateway(IWebProxy? webProxy, ILogger logger);

    LibGit2Sharp.Credentials CreateLibGit2SharpCredentials();
}
