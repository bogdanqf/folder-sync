# FolderSync

A one-way, periodic folder synchronization console application written in C# for .NET 8. No third-party dependencies.

## Run

Install the .NET 8 SDK, then run from the repository root:

```powershell
dotnet run --project src/FolderSync -- "C:\Sync\source" "C:\Sync\replica" 10 "C:\Sync\sync.log"
```

Linux/macOS example:

```sh
dotnet run --project src/FolderSync -- /tmp/source /tmp/replica 10 /tmp/sync.log
```

Exactly four positional arguments are required:

| Argument | Meaning |
| --- | --- |
| source | Existing source directory |
| replica | Destination directory; created if missing |
| interval-seconds | Positive number of seconds, decimal point supported |
| log-file | Append-only log outside both directories |

The first synchronization starts immediately. Subsequent passes start after the previous pass finishes plus the specified delay. Passes never overlap. Press Ctrl+C for graceful shutdown after the current pass.

**Replica contents may be overwritten or deleted. Use a dedicated replica directory.**

## Behavior

- Recursively creates directories, including empty ones, and copies new files.
- Compares file length and SHA-256 content hashes, detecting changes even when timestamps and file sizes are unchanged.
- Restores files modified in the replica and removes replica-only files and directories.
- Handles file/directory type conflicts.
- Logs successful creation, copying and deletion operations, pass completion and errors to both the console and a file with timestamps.
- Stages copied files next to the destination before replacing them, avoiding truncating the old file on copy failure.
- Rejects equal or nested source/replica paths and log paths inside either tree.
- Rejects symbolic links, junctions and other reparse points; checks existing ancestors too.
- Scans both trees before modifying the replica. A missing or unreadable source aborts the pass.
- Retries filesystem/access failures after the configured delay. Invalid startup configuration returns exit code 1.

## Verification

```sh
dotnet build src/FolderSync --configuration Release
dotnet run --project tests/FolderSync.Tests --configuration Release
```

The dependency-free integration test executable exits nonzero on failure. It covers nested files, empty directories, content changes with unchanged size/timestamp, unchanged files, replica edits, removals, type conflicts and path validation. GitHub Actions runs the same commands on Windows and Linux.

## Scope and limitations

“Identical” means the same relative file/directory structure and file bytes. ACLs, ownership, timestamps, alternate data streams and hard-link relationships are not mirrored. Use regular files and directories on a filesystem with compatible filename semantics. Do not run multiple instances against the same replica.

This is a periodic synchronizer, not a transactional snapshot system. Keep the source stable during a pass for a consistent result. Concurrent changes, filesystem failures or interruption can leave a partially synchronized replica; a subsequent successful pass converges it. Concurrent replacement of paths with links is unsupported. Ctrl+C waits for an active pass to finish.
