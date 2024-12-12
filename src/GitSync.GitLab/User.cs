using GitSync.GitProvider;
using NGitLab.Models;

namespace GitSync.GitLab;

sealed class User(Session user) : IUser
{
    public string Name => user.Name;
    public string? Email => user.Email;
}
