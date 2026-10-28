using System.Security.Cryptography;

namespace FolderSync;

public sealed class Synchronizer(string source, string replica, Action<string> log)
{
    private static readonly StringComparison Comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool Within(string path, string root) =>
        path.Equals(root, Comparison) || path.StartsWith(
            Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, Comparison);

    public static void ValidatePaths(string source, string replica, string log)
    {
        if (!Directory.Exists(source)) throw new ArgumentException("Source directory does not exist.");
        if (Within(source, replica) || Within(replica, source))
            throw new ArgumentException("Source and replica must be separate, non-nested directories.");
        if (Within(log, source) || Within(log, replica))
            throw new ArgumentException("Log file must be outside both synchronized directories.");
        CheckAncestors(source);
        CheckAncestors(replica);
        CheckAncestors(log);
    }

    private static void CheckAncestors(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            try { RejectLink(current); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Symbolic links and reparse points are unsupported: {path}");
    }

    // Complete the source inventory before any destructive replica operations.
    private static void Scan(string directory)
    {
        RejectLink(directory);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLink(entry);
            if (Directory.Exists(entry)) Scan(entry);
        }
    }

    public void Run()
    {
        CheckAncestors(source);
        CheckAncestors(replica);

        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(
                $"Source directory does not exist: {source}");

        Scan(source);

        if (Directory.Exists(replica))
            Scan(replica);

        SyncDirectory(source, replica);

        log("SYNC COMPLETE");
    }

    private void SyncDirectory(string from, string to)
    {
        RejectLink(from);
        if (File.Exists(to)) { File.Delete(to); log($"DELETE FILE: {to}"); }
        if (!Directory.Exists(to))
        {
            Directory.CreateDirectory(to);
            log($"CREATE DIRECTORY: {to}");
        }
        RejectLink(to);
        var entries = Directory.GetFileSystemEntries(from);
        var names = new HashSet<string>(entries.Select(p => Path.GetFileName(p)!),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            RejectLink(entry);
            var target = Path.Combine(to, Path.GetFileName(entry));
            if (Directory.Exists(entry)) { SyncDirectory(entry, target); continue; }
            if (Directory.Exists(target)) DeleteDirectory(target);
            if (!File.Exists(target) || !SameContent(entry, target))
            {
                CopyFile(entry, target);
                log($"COPY FILE: {entry} -> {target}");
            }
        }
        // Re-enumerate source before deletion: a disappeared source aborts this pass.
        _ = Directory.GetFileSystemEntries(from);
        foreach (var entry in Directory.GetFileSystemEntries(to))
        {
            if (names.Contains(Path.GetFileName(entry))) continue;
            RejectLink(entry);
            if (Directory.Exists(entry)) DeleteDirectory(entry);
            else { File.Delete(entry); log($"DELETE FILE: {entry}"); }
        }
    }

    private static bool SameContent(string a, string b)
    {
        RejectLink(b);
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        return SHA256.HashData(first).AsSpan().SequenceEqual(SHA256.HashData(second));
    }

    private static void CopyFile(string from, string to)
    {
        // Stage next to destination so a failed copy does not truncate the old file.
        var temporary = Path.Combine(Path.GetDirectoryName(to)!, $".foldersync-{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(from, temporary);
            File.Move(temporary, to, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void DeleteDirectory(string path)
    {
        RejectLink(path);
        foreach (var entry in Directory.GetFileSystemEntries(path))
        {
            RejectLink(entry);
            if (Directory.Exists(entry)) DeleteDirectory(entry);
            else { File.Delete(entry); log($"DELETE FILE: {entry}"); }
        }
        Directory.Delete(path);
        log($"DELETE DIRECTORY: {path}");
    }
}
