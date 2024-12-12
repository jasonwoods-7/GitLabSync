using GitSync.GitProvider;

namespace GitSync.GitLab;

sealed class NewTree(string parentPath) : INewTree
{
    public INewTreeItemCollection Tree { get; } = new NewTreeItemCollection(parentPath);
}
