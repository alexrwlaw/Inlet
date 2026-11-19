# Inlet

Inlet downloads one torrent at a time from a magnet link or a local `.torrent` file and shows progress in the terminal. It's built with .NET 10 and [MonoTorrent](https://github.com/alanmcgovern/monotorrent).

## Run Inlet

Install the .NET 10 SDK, then run this command from the repository root.

```sh
dotnet run --project Inlet
```

At the prompt, paste a magnet link or enter the path to a `.torrent` file. In Visual Studio, open `Inlet.slnx` and press **F5** to get the same prompt.

You can also pass the input directly. Quote the whole argument so spaces in paths and `&` characters in magnet links reach Inlet intact.

```powershell
dotnet run --project Inlet -- "C:\Torrents\example.torrent"
```

## Follow a download

A magnet link may show `Fetching metadata from peers...` while Inlet waits for the file list. During the download, the bar shows the share of pieces verified, followed by the download rate and connected peer count.

```text
example.iso (640.0 MiB)
[########------------]  40%  128/320 pieces verified  5.4 MiB/s  8 peers
```

Inlet saves files under `Downloads/<info-hash>/` in the working directory. Each torrent gets its own folder so downloads with the same filename can coexist. When the download finishes, Inlet prints the full path and exits.

Press **Ctrl+C** to stop. To resume, run Inlet from the same working directory with the same torrent file or magnet link. It verifies the pieces already on disk and downloads any that are missing or damaged.
