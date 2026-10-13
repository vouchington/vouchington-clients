using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Voucha.Client.Core.Agent;

/// <summary>One callback on an OS-assigned loopback port; no embedded browser or shared listener.</summary>
public sealed class MemberMcpLoopbackCallback : IAsyncDisposable
{
  private const string CallbackPath = "/oauth/native/windows/callback";
  private readonly TcpListener listener;
  private readonly CancellationTokenSource lifetime = new();
  private int consumed;

  private MemberMcpLoopbackCallback(TcpListener listener)
  {
    this.listener = listener;
    var address = (IPEndPoint)listener.LocalEndpoint;
    RedirectUri = new UriBuilder(Uri.UriSchemeHttp, address.Address.ToString(), address.Port, CallbackPath).Uri;
  }

  public Uri RedirectUri { get; }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The callback owns the started listener; failed listeners are stopped before the next address.")]
  public static MemberMcpLoopbackCallback Start()
  {
    foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
    {
      var listener = new TcpListener(address, 0) { ExclusiveAddressUse = true };
      try
      {
        listener.Start(1);
        return new MemberMcpLoopbackCallback(listener);
      }
      catch (SocketException)
      {
        listener.Stop();
      }
    }
    throw new SocketException((int)SocketError.AddressNotAvailable);
  }

  public async Task<Uri> ReceiveAsync(
      string expectedState, string expectedIssuerIdentifier, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(expectedState);
    ArgumentException.ThrowIfNullOrWhiteSpace(expectedIssuerIdentifier);
    if (Interlocked.Exchange(ref consumed, 1) != 0)
      throw new InvalidOperationException("The OAuth callback is one-shot.");
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
    try
    {
      while (true)
      {
        using var client = await listener.AcceptTcpClientAsync(linked.Token).ConfigureAwait(false);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        attempt.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
          var result = await ReadCallbackAsync(
              client, expectedState, expectedIssuerIdentifier, attempt.Token).ConfigureAwait(false);
          if (result is not null) return result;
        }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested)
        {
          // An idle unrelated connection must not consume the OAuth callback.
        }
        catch (IOException) when (!linked.IsCancellationRequested)
        {
          // A disconnected prefetch or local probe is not the OAuth redirect.
        }
        catch (SocketException) when (!linked.IsCancellationRequested)
        {
          // The peer may close before receiving its 400 response.
        }
      }
    }
    finally
    {
      listener.Stop();
    }
  }

  private async Task<Uri?> ReadCallbackAsync(
      TcpClient client, string expectedState, string expectedIssuerIdentifier, CancellationToken cancellationToken)
  {
    using var stream = client.GetStream();
    var bytes = new byte[8_192];
    var count = 0;
    while (count < bytes.Length)
    {
      var read = await stream.ReadAsync(bytes.AsMemory(count, 1), cancellationToken).ConfigureAwait(false);
      if (read == 0) break;
      count += read;
      if (count >= 4 && bytes.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8)) break;
    }
    var request = Encoding.ASCII.GetString(bytes, 0, count);
    var line = request.Split("\r\n", 2, StringSplitOptions.None)[0].Split(' ');
    var valid = count < bytes.Length && request.EndsWith("\r\n\r\n", StringComparison.Ordinal) &&
        line.Length == 3 && line[0] == "GET" && line[2].StartsWith("HTTP/1.", StringComparison.Ordinal) &&
        line[1].StartsWith(CallbackPath + "?", StringComparison.Ordinal);
    var callback = valid ? new Uri(RedirectUri, line[1]) : null;
    valid = callback is not null && IsExpectedCallback(callback, expectedState, expectedIssuerIdentifier);
    var body = Encoding.UTF8.GetBytes(valid ? "You may return to Voucha." : "Invalid OAuth callback.");
    var header = Encoding.ASCII.GetBytes(
        $"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
    await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    return valid ? callback : null;
  }

  private static bool IsExpectedCallback(Uri callback, string expectedState, string expectedIssuerIdentifier)
  {
    var pairs = callback.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .Select(parts => (Key: Uri.UnescapeDataString(parts[0]), Value: Uri.UnescapeDataString(parts[1])))
        .ToArray();
    if (pairs.GroupBy(pair => pair.Key).Any(group => group.Count() > 1)) return false;
    var values = pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
    return values.TryGetValue("state", out var state) && state == expectedState &&
        values.TryGetValue("iss", out var issuer) && issuer == expectedIssuerIdentifier &&
        values.ContainsKey("code") != values.ContainsKey("error") &&
        (values.TryGetValue("code", out var code) ? !string.IsNullOrEmpty(code) :
            values.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error));
  }

  public async ValueTask DisposeAsync()
  {
    await lifetime.CancelAsync().ConfigureAwait(false);
    listener.Stop();
    lifetime.Dispose();
  }
}
