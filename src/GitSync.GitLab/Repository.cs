using GitSync.GitProvider;
using NGitLab.Models;

namespace GitSync.GitLab;

sealed class Repository(Project project) : IRepository
{
    public IOwner Owner => new Owner(project.Owner);
    public string CloneUrl => project.HttpUrl;
}
