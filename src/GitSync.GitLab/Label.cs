using GitSync.GitProvider;

namespace GitSync.GitLab;

sealed class Label(string label) : ILabel
{
    public string Name => label;
}
