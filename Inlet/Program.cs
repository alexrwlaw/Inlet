using MonoTorrent;
using MonoTorrent.Client;

const string usage = "Usage: Inlet [\"magnet link\" | \"path to .torrent file\"]";
if (args is ["--help"] or ["-h"])
{
    Console.WriteLine(usage);
    return 0;
}
if (args.Length > 1)
{
    Console.Error.WriteLine(usage);
    return 2;
}

string? input = args.FirstOrDefault();
if (input is null)
{
    Console.Write("Magnet link or torrent file> ");
    input = Console.ReadLine();
}

input = input?.Trim().Trim('"');
if (string.IsNullOrWhiteSpace(input))
{
    Console.Error.WriteLine("Enter a magnet link or a .torrent file path.");
    return 2;
}

MagnetInput? magnet = null;
if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
{
    if (!MagnetInput.TryParse(input, out magnet))
    {
        Console.Error.WriteLine("Invalid magnet link. Include a BitTorrent info hash in the link.");
        return 2;
    }
}
else if (!File.Exists(input))
{
    Console.Error.WriteLine($"Torrent file not found: {input}");
    return 2;
}

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += onCancel;

try
{
    var torrent = magnet is null ? await Torrent.LoadAsync(input) : null;
    var hashes = torrent?.InfoHashes ?? magnet!.Link.InfoHashes;
    string downloadId = hashes.V1OrV2.ToHex().ToLowerInvariant();
    string destination = Path.GetFullPath(Path.Combine("Downloads", downloadId));
    string cache = Directory.CreateDirectory(Path.Combine(".inlet", downloadId)).FullName;
    // The open handle protects the payload and its metadata even across separate Inlet processes.
    using var downloadLock = File.Open(Path.Combine(cache, "download.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var settings = new EngineSettingsBuilder
    {
        CacheDirectory = cache,
        AutoSaveLoadFastResume = false,
        AllowPortForwarding = false
    };
    using var engine = new ClientEngine(settings.ToSettings());
    var manager = torrent is null
        ? await engine.AddAsync(magnet!.Link, destination)
        : await engine.AddAsync(torrent, destination);

    Console.WriteLine($"Downloading to {destination}");
    Console.WriteLine("Press Ctrl+C to stop and resume later.");
    try
    {
        cancellation.Token.ThrowIfCancellationRequested();
        if (magnet is not null)
            await magnet.AddPeersAsync(manager, cancellation.Token);
        await manager.StartAsync();
        await DownloadProgress.ShowAsync(manager, cancellation.Token);
    }
    finally
    {
        await manager.StopAsync(TimeSpan.FromSeconds(2));
    }

    var savedPath = manager.Files.Count == 1
        ? manager.Files[0].DownloadCompleteFullPath
        : manager.ContainingDirectory;
    Console.WriteLine($"Saved to {savedPath}");
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Stopped. Run again from this folder with the same torrent to resume.");
    return 130;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Download failed: {error.Message}");
    return 1;
}
finally
{
    Console.CancelKeyPress -= onCancel;
}
