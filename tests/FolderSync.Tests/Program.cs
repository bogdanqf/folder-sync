using FolderSync;

var root = Path.Combine(Path.GetTempPath(), "folder-sync-tests-" + Guid.NewGuid().ToString("N"));
var source = Path.Combine(root, "source");
var replica = Path.Combine(root, "replica");
var messages = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
void Rejected(Action action, string name)
{
    try { action(); }
    catch (ArgumentException) { Check(true, name); return; }
    throw new Exception("FAIL: " + name);
}
try
{
    Directory.CreateDirectory(Path.Combine(source, "nested", "empty"));
    File.WriteAllText(Path.Combine(source, "nested", "file.txt"), "original");
    var sync = new Synchronizer(source, replica, messages.Add);
    sync.Run();
    var file = Path.Combine(replica, "nested", "file.txt");
    Check(File.ReadAllText(file) == "original", "initial recursive copy");
    Check(Directory.Exists(Path.Combine(replica, "nested", "empty")), "empty directories");
    var sourceFile = Path.Combine(source, "nested", "file.txt");
    var timestamp = File.GetLastWriteTimeUtc(sourceFile);
    File.WriteAllText(sourceFile, "modified");
    File.SetLastWriteTimeUtc(sourceFile, timestamp);
    sync.Run();
    Check(File.ReadAllText(file) == "modified", "same size and timestamp content change");
    var copies = messages.Count(m => m.StartsWith("COPY FILE:"));
    sync.Run();
    Check(messages.Count(m => m.StartsWith("COPY FILE:")) == copies, "unchanged file is not copied");
    File.WriteAllText(file, "tampered");
    Directory.CreateDirectory(Path.Combine(replica, "extra"));
    File.WriteAllText(Path.Combine(replica, "extra", "delete.txt"), "extra");
    sync.Run();
    Check(File.ReadAllText(file) == "modified", "replica edits overwritten");
    Check(!Directory.Exists(Path.Combine(replica, "extra")), "extra subtree removed");
    File.Delete(sourceFile);
    sync.Run();
    Check(!File.Exists(file), "source deletion propagated");
    File.WriteAllText(Path.Combine(source, "conflict"), "file");
    Directory.CreateDirectory(Path.Combine(replica, "conflict"));
    sync.Run();
    Check(File.Exists(Path.Combine(replica, "conflict")), "directory replaced by file");
    File.Delete(Path.Combine(source, "conflict"));
    Directory.CreateDirectory(Path.Combine(source, "conflict"));
    sync.Run();
    Check(Directory.Exists(Path.Combine(replica, "conflict")), "file replaced by directory");
    Rejected(() => Synchronizer.ValidatePaths(source, source, Path.Combine(root, "log")), "equal paths rejected");
    Rejected(() => Synchronizer.ValidatePaths(source, Path.Combine(source, "child"), Path.Combine(root, "log")), "nested paths rejected");
    Rejected(() => Synchronizer.ValidatePaths(source, replica, Path.Combine(replica, "log")), "internal log rejected");
    Directory.Delete(source, true);
    try { sync.Run(); throw new Exception("FAIL: missing source accepted"); }
    catch (DirectoryNotFoundException) { Check(Directory.Exists(replica), "missing source does not erase replica"); }
    Check(messages.Any(m => m.StartsWith("DELETE FILE:")), "deletions logged");
    Console.WriteLine("All checks passed.");
    return 0;
}
finally { Directory.Delete(root, true); }
