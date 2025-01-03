namespace GitSync.GitProvider;

public interface IRepository
{
    IOwner Owner { get; }
    string CloneUrl { get; }
}
