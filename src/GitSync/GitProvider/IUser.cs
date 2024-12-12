namespace GitSync.GitProvider;

public interface IUser
{
    string Name { get; }
    string? Email { get; }
}
