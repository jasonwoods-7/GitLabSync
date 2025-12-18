using GitSync.GitProvider;

namespace GitSync.GitLab;

sealed class TreeResponse(string path, IEnumerable<NGitLab.Models.Tree> tree) : ITreeResponse
{
    public string Path { get; } = path;
    public IReadOnlyList<ITreeItem> Tree { get; } =
        tree.Select(t => new TreeItem(t)).ToList<ITreeItem>();
}
