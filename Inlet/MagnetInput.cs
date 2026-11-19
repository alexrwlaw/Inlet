using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using MonoTorrent;
using MonoTorrent.Client;

internal sealed class MagnetInput(MagnetLink link, IReadOnlyList<Uri> peerHints)
{
    public MagnetLink Link { get; } = link;

    public static bool TryParse(string input, [NotNullWhen(true)] out MagnetInput? magnet)
    {
        magnet = null;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "magnet" || uri.Query.Length < 2)
            return false;

        var parameters = new List<string>();
        var peers = new List<Uri>();
        foreach (string parameter in uri.Query[1..].Split('&'))
        {
            int separator = parameter.IndexOf('=');
            if (separator < 2)
                continue;

            string key = parameter[..separator];
            if (key == "xt" || key.StartsWith("xt.", StringComparison.Ordinal))
            {
                // MonoTorrent expects literal colons in the URN. Keep query delimiters escaped.
                string topic = Uri.UnescapeDataString(parameter[(separator + 1)..]);
                parameters.Add("xt=" + Uri.EscapeDataString(topic).Replace("%3A", ":"));
            }
            else if (key == "x.pe")
            {
                string endpoint = Uri.UnescapeDataString(parameter[(separator + 1)..]);
                if (Uri.TryCreate("tcp://" + endpoint, UriKind.Absolute, out var peer)
                    && peer.Port > 0 && peer.UserInfo.Length == 0 && peer.AbsolutePath == "/"
                    && peer.Query.Length == 0 && peer.Fragment.Length == 0
                    && peer.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 or UriHostNameType.Dns)
                    peers.Add(peer);
            }
            else
            {
                parameters.Add(parameter);
            }
        }

        if (!MagnetLink.TryParse("magnet:?" + string.Join('&', parameters), out var parsed))
            return false;

        magnet = new MagnetInput(parsed, peers);
        return true;
    }

    public async Task AddPeersAsync(TorrentManager manager, CancellationToken token)
    {
        if (peerHints.Count == 0 || manager.Torrent?.IsPrivate == true)
            return;

        var peers = new HashSet<Uri>();
        foreach (var hint in peerHints.Distinct())
        {
            token.ThrowIfCancellationRequested();
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(hint.IdnHost, token);
            }
            catch (Exception error) when (error is SocketException or ArgumentException)
            {
                // An unavailable optional peer must not prevent normal peer discovery.
                continue;
            }

            foreach (var address in addresses)
            {
                string scheme = address.AddressFamily == AddressFamily.InterNetworkV6 ? "ipv6" : "ipv4";
                peers.Add(new Uri($"{scheme}://{new IPEndPoint(address, hint.Port)}"));
            }
        }

        if (peers.Count > 0)
            await manager.AddPeersAsync(peers.Select(peer => new PeerInfo(peer)));
    }
}
