namespace GitSync.GitProvider;

public interface ITreeResponse
{
    string Path { get; }
    IReadOnlyList<ITreeItem> Tree { get; }
}
