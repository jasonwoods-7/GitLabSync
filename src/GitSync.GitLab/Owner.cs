using GitSync.GitProvider;

namespace GitSync.GitLab;

sealed class Owner(NGitLab.Models.User owner) : IOwner
{
    public string Login => owner.Username;
}
