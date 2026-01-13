namespace GitSync.GitLab.Tool.Config;

sealed class Repository
{
    public string Name { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string Branch { get; set; } = "master";

    public bool AutoMerge { get; set; }

    public List<string> Templates { get; set; } = [];

    public List<string> Labels { get; set; } = [];

    public override string ToString() =>
        $"{this.Name} ({this.Url})";
}
