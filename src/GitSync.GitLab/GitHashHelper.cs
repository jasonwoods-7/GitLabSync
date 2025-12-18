using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using GitSync.GitProvider;
using NGitLab.Models;

namespace GitSync.GitLab;

static class GitHashHelper
{
    const string GitLabHashFormat = "X2";
    static readonly Encoding Encoding = new UTF8Encoding(false);

    public static async Task<string> GetBlobHash(Stream blob)
    {
        using var stream = new MemoryStream();
        await stream.WriteAsync($"blob {blob.Length}\0");
        await stream.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return await ComputeHash(stream, blob);
    }

    public static async Task<string> GetTreeHash(INewTree newTree)
    {
        var orderedItems = newTree.Tree.OrderBy(t => t.Name).ToList();

        using var treeStream = new MemoryStream();

        foreach (var item in orderedItems)
        {
            await treeStream.WriteAsync($"{item.Mode.TrimStart('0')} {item.Name}\0");

            treeStream.Write(ConvertShaToSpan(item.Sha));
        }

        await treeStream.FlushAsync();
        treeStream.Seek(0, SeekOrigin.Begin);

        using var stream = new MemoryStream();
        await stream.WriteAsync($"tree {treeStream.Length}\0");
        await stream.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return await ComputeHash(stream, treeStream);
    }

    public static async Task<string> GetCommitHash(
        string treeSha,
        string parentCommitSha,
        string commitMessage,
        Session user
    )
    {
        var usernameAndDate =
            $"{user.Username} <{user.Email}> {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} +0000";

        var commitData = new MemoryStream();
        await commitData.WriteAsync($"tree {treeSha}\n");
        await commitData.WriteAsync($"parent {parentCommitSha}\n");
        await commitData.WriteAsync($"author {usernameAndDate}\n");
        await commitData.WriteAsync($"committer {usernameAndDate}\n\n");
        await commitData.WriteAsync(commitMessage);
        await commitData.FlushAsync();
        commitData.Seek(0, SeekOrigin.Begin);

        using var stream = new MemoryStream();
        await stream.WriteAsync($"commit {commitData.Length}\0");
        await stream.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return await ComputeHash(stream, commitData);
    }

    static async Task<string> ComputeHash(params Stream[] streams)
    {
        Debug.Assert(streams.All(s => s.Position == 0));

#pragma warning disable CA5350 // Do not use weak cryptographic hashing algorithm
        using var sha1 = System.Security.Cryptography.SHA1.Create();
#pragma warning restore

        const int length = 64 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            var offset = 0;
            foreach (var stream in streams)
            {
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, length - offset))) > 0)
                {
                    offset =
                        (offset + sha1.TransformBlock(buffer, 0, read, buffer, offset)) % length;
                }
            }

            sha1.TransformFinalBlock(buffer, 0, 0);
            var hash = sha1.Hash!.Aggregate(
                new StringBuilder(),
                (s, b) => s.Append(b.ToString(GitLabHashFormat, CultureInfo.InvariantCulture)),
                s => s.ToString()
            );

            return hash;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static async Task WriteAsync(this Stream stream, string value)
    {
        var buffer = Encoding.GetBytes(value);
        await stream.WriteAsync(buffer);
    }

    static ReadOnlySpan<byte> ConvertShaToSpan(string sha)
    {
        Debug.Assert(sha.Length == 40);

        var buffer = new byte[20];

        for (var i = 0; i < 40; i += 2)
        {
            buffer[i >> 1] = byte.Parse(
                sha.AsSpan(i, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture
            );
        }

        return buffer;
    }
}
