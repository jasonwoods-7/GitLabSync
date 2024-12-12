namespace GitSync;

public class Parts : IParts
{
    public Parts(string owner, string repository, TreeEntryTargetType type, string branch, string? path, string? sha = null, string? mode = null)
    {
        this.Owner = owner;
        this.Repository = repository;
        this.Type = type;
        this.Branch = branch;
        this.Path = path;
        this.Sha = sha;
        this.Mode = mode;

        this.Url = string.Join('/', "https://github.com", owner, repository, type.ToString().ToLowerInvariant(), branch);

        if (path == null)
        {
            this.Name = null;
            this.NumberOfPathSegments = 0;
        }
        else
        {
            this.Url = string.Join('/', this.Url, path);
            var segments = path.Split('/');
            this.Name = segments.Last();
            this.NumberOfPathSegments = segments.Length;
        }
    }

    public static readonly NullParts Empty = new();

    public string Owner { get; }
    public string Repository { get; }
    public TreeEntryTargetType Type { get; }
    public string Branch { get; }
    public string? Path { get; }

    public string? Name { get; }

    // This doesn't participate as an equality contributor on purpose
    public int NumberOfPathSegments { get; }

    // This doesn't participate as an equality contributor on purpose
    public string Url { get; }

    // This doesn't participate as an equality contributor on purpose
    public string? Sha { get; }

    // This doesn't participate as an equality contributor on purpose
    public string? Mode { get; }

    public Parts Combine(TreeEntryTargetType type, string name, string sha, string mode) =>
        new(this.Owner, this.Repository, type, this.Branch, this.Path == null ? name : this.Path + "/" + name, sha, mode);

    public class NullParts : IParts
    {
        internal NullParts()
        { }
    }
}
