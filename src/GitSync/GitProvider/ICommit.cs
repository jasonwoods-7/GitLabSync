namespace GitSync.GitProvider;

public interface ICommit
{
    string Sha { get; }
    ITree Tree { get; }
}
