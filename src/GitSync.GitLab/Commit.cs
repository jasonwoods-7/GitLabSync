using NGitLab.Models;

namespace GitSync.GitLab;

sealed class Commit : ICommit
{
    Commit(string commitSha, string treeSha)
    {
        this.Sha = commitSha;
        this.Tree = new Tree(treeSha);
    }

    public static async Task<Commit> CreateAsync(CommitInfo info, NGitLab.IRepositoryClient repositoryClient)
    {
        var commitSha = info.Id.ToString();

        var tree = repositoryClient
            .GetTreeAsync(new() { Ref = commitSha })
            .Aggregate(
                new NewTree(""),
                (nt, ct) =>
                {
                    nt.Tree.Add(ct.Mode, ct.Name, ct.Id.ToString(), ct.Type switch
                    {
                        ObjectType.blob => TreeType.Blob,
                        ObjectType.tree => TreeType.Tree,
                        ObjectType.commit => throw new NotImplementedException(),
                        _ => throw new InvalidOperationException()
                    });

                    return nt;
                });

        var treeSha = await GitHashHelper.GetTreeHash(tree);

        return new(commitSha, treeSha);
    }

    public string Sha { get; }
    public ITree Tree { get; }
}
