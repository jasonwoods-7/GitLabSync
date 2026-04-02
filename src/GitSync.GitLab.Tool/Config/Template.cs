namespace GitSync.GitLab.Tool.Config;

sealed class Template
{
    public string name { get; set; } = null!;

    public string url { get; set; } = null!;

    public string branch { get; set; } = "main";

    public List<string> ignore { get; set; } = [];

    public override string ToString() => $"{this.name} ({this.url})";
}
