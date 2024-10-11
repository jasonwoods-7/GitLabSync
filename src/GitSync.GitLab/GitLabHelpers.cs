using NGitLab;

namespace GitSync.GitLab;

static class GitLabHelpers
{
    static readonly Dictionary<string, int> projectIds = [];

    public static async Task<int> GetProjectId(this IGitLabClient client, string owner, string name)
    {
        var key = $"{owner}/{name}";

        if (!projectIds.TryGetValue(key, out var id))
        {
            id = (await client
                    .Projects
                    .GetByNamespacedPathAsync(key))
                .Id;

            projectIds[key] = id;
        }

        return id;
    }
}
