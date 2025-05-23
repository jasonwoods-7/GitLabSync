namespace GitSync.GitLab.Tool;

static class UrlHelper
{
    public static string GetCompany(string url)
    {
        var uri = new Uri(url);
        return uri.PathAndQuery[1..].Split('/').First();
    }

    public static string GetProject(string url)
    {
        var uri = new Uri(url);
        return uri.PathAndQuery.Split('/', 3).Last();
    }
}
