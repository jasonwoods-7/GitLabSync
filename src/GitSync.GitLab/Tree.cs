using GitSync.GitProvider;

namespace GitSync.GitLab;

sealed class Tree(string sha) : ITree
{
    public string Sha { get; } = sha;
}
