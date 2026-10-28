using System.Globalization;
using FolderSync;

if (args.Length != 4 || !double.TryParse(args[2], NumberStyles.Float,
        CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) ||
    seconds <= 0 || seconds > int.MaxValue / 1000d)
{
    Console.Error.WriteLine("Usage: FolderSync <source> <replica> <interval-seconds> <log-file>");
    return 1;
}

try
{
    var source = Path.GetFullPath(args[0]);
    var replica = Path.GetFullPath(args[1]);
    var log = Path.GetFullPath(args[3]);
    Synchronizer.ValidatePaths(source, replica, log);
    Directory.CreateDirectory(Path.GetDirectoryName(log)!);
    using var writer = new StreamWriter(log, append: true) { AutoFlush = true };
    void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}";
        Console.WriteLine(line);
        writer.WriteLine(line);
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var sync = new Synchronizer(source, replica, Log);
    Log("START: Press Ctrl+C to stop.");
    while (!cancellation.IsCancellationRequested)
    {
        try { sync.Run(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"ERROR: {ex.Message}; retrying after the interval.");
        }
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), cancellation.Token); }
        catch (OperationCanceledException) { break; }
    }
    Log("STOP");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
