namespace GitSync.GitLab;

sealed class NewTreeItemCollection(string parentPath)
    : List<INewTreeItem>
    , INewTreeItemCollection
{
    public void Add(string mode, string name, string sha, TreeType type) =>
        this.Add(new NewTreeItem(mode, $"{parentPath}{name}", name, sha, type));
}
