using System.Diagnostics;
using GitSync.GitProvider;

namespace GitSync.GitLab;

[DebuggerDisplay("{Mode} {Type} {Sha} {Path}")]
sealed class NewTreeItem(string mode, string path, string name, string sha, TreeType type) : INewTreeItem
{
    public string Mode { get; } = mode;
    public string Path { get; } = path;
    public string Name { get; } = name;
    public string Sha { get; } = sha;
    public TreeType Type { get; } = type;
}
