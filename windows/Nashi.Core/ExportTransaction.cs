namespace Nashi.Core;

/// Stages beside the destination. A failed/cancelled batch publishes nothing and
/// removes only files owned by this transaction; originals are never overwritten.
public sealed class ExportTransaction : IDisposable
{
    public string StagingDirectory { get; }
    public string Destination { get; }
    private readonly List<string> published = [];
    private bool committed;

    public ExportTransaction(string destination)
    {
        Destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(Destination);
        StagingDirectory = Path.Combine(Destination, ".nashi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(StagingDirectory);
        if ((File.GetAttributes(StagingDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Staging cannot be a symbolic link.");
        // A leading dot hides it on macOS; Windows needs the attribute.
        File.SetAttributes(StagingDirectory, File.GetAttributes(StagingDirectory) | FileAttributes.Hidden);
    }

    public IReadOnlyList<string> Publish(IEnumerable<string> outputs, CancellationToken token)
    {
        var files = outputs.ToList();
        if (files.Count == 0) throw new InvalidDataException("No output was produced.");
        foreach (var item in files)
        {
            token.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(item);
            if (!full.StartsWith(StagingDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("An output is outside its staging directory.");
            if (!Directory.Exists(full) && (!File.Exists(full) || new FileInfo(full).Length == 0))
                throw new InvalidDataException("An empty output was produced.");
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("An output cannot be a symbolic link.");
        }
        foreach (var item in files)
        {
            token.ThrowIfCancellationRequested();
            var destination = SafePaths.Unique(Destination, Path.GetFileName(item));
            if (Directory.Exists(item)) Directory.Move(item, destination);
            else File.Move(item, destination, overwrite: false);
            published.Add(destination);
        }
        token.ThrowIfCancellationRequested();
        committed = true;
        return published.ToArray();
    }

    /// Cleanup never replaces the batch's real outcome. A tool killed on cancel can hold a
    /// file for a moment (yt-dlp's ffmpeg child, for example), which used to turn "Cancelled"
    /// into "file is being used by another process" and leave the staging folder behind.
    /// Whatever is still locked is retried in the background, so callers on the UI thread
    /// never wait for it.
    public void Dispose()
    {
        var leftovers = (committed ? [] : published).Append(StagingDirectory).Where(path => !TryDelete(path)).ToArray();
        if (leftovers.Length == 0) return;
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 40 && leftovers.Length > 0; attempt++)
            {
                await Task.Delay(250);
                leftovers = leftovers.Where(path => !TryDelete(path)).ToArray();
            }
        });
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
