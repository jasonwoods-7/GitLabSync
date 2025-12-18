namespace GitSync.GitLab;

public class GitSyncException : Exception
{
    public GitSyncException(string message)
        : base(message) { }

    public GitSyncException(string message, Exception innerException)
        : base(message, innerException) { }
}
