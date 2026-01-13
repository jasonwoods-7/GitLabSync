using Microsoft.Extensions.Logging;

namespace GitSync.GitLab;

static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Ctor - Create temp blob storage '{Path}'"
    )]
    public static partial void CreateTempBlobStorage(this ILogger logger, string path);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Dispose - Delete temp blob storage '{BlobStoragePath}'"
    )]
    public static partial void DeleteTempBlobStorage(this ILogger logger, string blobStoragePath);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "API Query - Retrieve blob '{Sha}' details from '{Owner}/{Repository}'"
    )]
    public static partial void RetrieveBlobDetails(
        this ILogger logger,
        string sha,
        string owner,
        string repository
    );
}
