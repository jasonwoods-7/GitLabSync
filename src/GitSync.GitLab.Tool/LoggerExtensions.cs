using GitSync.GitLab.Tool.Config;
using Microsoft.Extensions.Logging;

namespace GitSync.GitLab.Tool;

static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "No environment variable 'GitLab_OAuthToken' found")]
    public static partial void NoGitLabToken(
        this ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Path does not exist: {Path}")]
    public static partial void PathDoesNotExist(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "{Prefix} Setting up synchronization for '{TargetRepository}'")]
    public static partial void SettingUpSynchronization(
        this ILogger logger,
        string prefix,
        Repository targetRepository);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "{Prefix} Synchronized '{TargetRepository}', took {Time}")]
    public static partial void Synchronized(
        this ILogger logger,
        string prefix,
        Repository targetRepository,
        string time);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Failed to synchronize '{TargetRepository}'")]
    public static partial void FailedToSynchronize(
        this ILogger logger,
        Exception exception,
        Repository targetRepository);
}
