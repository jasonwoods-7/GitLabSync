using Microsoft.Extensions.Logging;

namespace GitSync;

static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Diff - Analyze {SourceType} source '{SourceUrl}'"
    )]
    public static partial void AnalyzeSource(
        this ILogger logger,
        TreeEntryTargetType sourceType,
        string sourceUrl
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Diff - Analyze {SourceType} target '{DestinationUrl}'"
    )]
    public static partial void AnalyzeTarget(
        this ILogger logger,
        TreeEntryTargetType sourceType,
        string destinationUrl
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Diff - No sync required. Matching sha ({SourceSha}) between target '{DestinationUrl}' and source '{SourceUrl}."
    )]
    public static partial void NoSyncRequired(
        this ILogger logger,
        string sourceSha,
        string sourceUrl,
        string destinationUrl
    );

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Diff - {Action} required. Non-matching sha ({SourceSha} vs {DestinationSha}) between target '{DestinationUrl}' and source '{SourceUrl}."
    )]
    public static partial void SyncRequired(
        this ILogger logger,
        string action,
        string sourceSha,
        string destinationSha,
        string destinationUrl,
        string sourceUrl
    );

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Cannot create pull request, there is an existing open pull request, close or merge that first"
    )]
    public static partial void CannotCreatePullRequest(this ILogger logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "User is not a collaborator, need to create a fork"
    )]
    public static partial void UserIsNotCollaborator(this ILogger logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Debug,
        Message = "Sync - Determine if Blob '{BlobSha}' requires to be created in '{DestinationOwner}/{DestinationRepository}'"
    )]
    public static partial void DetermineIfBlobRequiresCreation(
        this ILogger logger,
        string blobSha,
        string destinationOwner,
        string destinationRepository
    );

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Debug,
        Message = "Sync - Determine if Tree '{TreeSha}' requires to be created in '{DestinationOwner}/{DestinationRepository}'"
    )]
    public static partial void DetermineIfTreeRequiresCreation(
        this ILogger logger,
        string treeSha,
        string destinationOwner,
        string destinationRepository
    );

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Mapping '{SourceUrl}' => '{DestinationUrl}'"
    )]
    public static partial void Mapping(
        this ILogger logger,
        string sourceUrl,
        string destinationUrl
    );

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Debug,
        Message = "Repo {TargetRepositoryDisplayName} is in sync"
    )]
    public static partial void RepoInSync(this ILogger logger, string targetRepositoryDisplayName);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "Pull created for {TargetRepositoryDisplayName}, click here to review and pull: {CreatedSyncBranch}"
    )]
    public static partial void PullCreated(
        this ILogger logger,
        string targetRepositoryDisplayName,
        UpdateResult createdSyncBranch
    );
}
