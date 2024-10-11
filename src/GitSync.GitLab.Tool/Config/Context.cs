namespace GitSync.GitLab.Tool.Config;

sealed class Context
{
    public List<Template> Templates { get; set; } = [];

    public List<Repository> Repositories { get; set; } = [];
}
