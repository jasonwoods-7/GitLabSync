namespace GitSync;

public class GitSyncException
(
    string message,
    Exception? ex = null
) : ApplicationException(message, ex);
