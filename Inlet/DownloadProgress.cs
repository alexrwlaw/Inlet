using MonoTorrent;
using MonoTorrent.Client;

internal static class DownloadProgress
{
    public static async Task ShowAsync(TorrentManager manager, CancellationToken token)
    {
        bool printedName = false;
        bool interactive = !Console.IsOutputRedirected;
        string previous = "";

        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var state = manager.State;
                if (state == TorrentState.Error)
                    throw new IOException(manager.Error?.Exception.Message ?? "The torrent entered an error state.");

                if (!printedName && manager.Torrent is { } torrent)
                {
                    if (interactive && previous.Length > 0)
                        Console.WriteLine();
                    var name = string.Concat(torrent.Name.Where(c => !char.IsControl(c)));
                    var payloadSize = torrent.Files.Sum(file => file.Length);
                    Console.WriteLine($"{name} ({FormatBytes(payloadSize)})");
                    printedName = true;
                    previous = "";
                }

                string status = state switch
                {
                    _ when !manager.HasMetadata => "Fetching metadata from peers...",
                    TorrentState.FetchingHashes => "Fetching piece hashes from peers...",
                    TorrentState.Hashing => "Checking existing files...",
                    TorrentState.Starting => "Starting download...",
                    _ => FormatProgress(manager)
                };
                if (interactive)
                {
                    int width = Math.Max(1, Console.WindowWidth - 1);
                    status = status[..Math.Min(status.Length, width)];
                    int clearTo = Math.Min(previous.Length, width);
                    Console.Write("\r" + status.PadRight(Math.Max(status.Length, clearTo)));
                }
                else if (status != previous)
                {
                    Console.WriteLine(status);
                }
                previous = status;

                if (state == TorrentState.Seeding)
                    return;
                await Task.Delay(500, token);
            }
        }
        finally
        {
            if (interactive && previous.Length > 0)
                Console.WriteLine();
        }
    }

    private static string FormatProgress(TorrentManager manager)
    {
        const int barWidth = 20;
        var pieces = manager.Bitfield;
        int verified = pieces.TrueCount;
        long percent = pieces.Length == 0 ? 100 : 100L * verified / pieces.Length;
        int filled = (int)(percent * barWidth / 100);
        var bar = new string('#', filled) + new string('-', barWidth - filled);
        int peers = manager.Peers.Seeds + manager.Peers.Leechs;
        return $"[{bar}] {percent,3}%  {verified}/{pieces.Length} pieces verified  " +
               $"{FormatBytes(manager.Monitor.DownloadRate)}/s  {peers} {(peers == 1 ? "peer" : "peers")}";
    }

    private static string FormatBytes(double bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (1L << 40):0.0} TiB",
        >= 1L << 30 => $"{bytes / (1L << 30):0.0} GiB",
        >= 1L << 20 => $"{bytes / (1L << 20):0.0} MiB",
        >= 1L << 10 => $"{bytes / (1L << 10):0.0} KiB",
        _ => $"{bytes:0} B"
    };
}
