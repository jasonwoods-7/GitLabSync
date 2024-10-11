using System.Diagnostics;
using NGitLab.Models;

namespace GitSync.GitLab;

[DebuggerDisplay("{Mode} {Type} {Sha} {Path}")]
sealed class TreeItem(NGitLab.Models.Tree tree) : ITreeItem
{
    public string Mode { get; } = tree.Mode;
    public string Path { get; } = tree.Path;
    public string Name { get; } = tree.Name;
    public string Sha { get; } = tree.Id.ToString();

    public TreeType Type { get; } = tree.Type switch
    {
        ObjectType.blob => TreeType.Blob,
        ObjectType.tree => TreeType.Tree,
        ObjectType.commit => throw new NotImplementedException(),
        _ => throw new NotImplementedException()
    };
}
