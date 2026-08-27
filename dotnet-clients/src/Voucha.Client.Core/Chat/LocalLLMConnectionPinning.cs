using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Voucha.Client.Core.Chat;

/// <summary>
/// Re-validates a local-LLM endpoint's actually-resolved peer address at connect time, wired in as
/// <see cref="SocketsHttpHandler.ConnectCallback"/>. <see cref="LocalLLMEndpointProfile"/> only checks
/// the configured hostname when a profile is saved; DNS can resolve that same hostname to a different
/// -- possibly public -- address by the time the client connects. This closes that gap by re-applying
/// <see cref="LocalLLMNetworkPolicy"/> to the resolved address itself before any cleartext data is sent.
/// </summary>
internal static class LocalLLMConnectionPinning
{
  /// <summary>Bounds how long a non-final candidate gets before falling through to the next one. Only
  /// the last remaining candidate connects with no artificial bound (beyond the caller's own
  /// cancellation), since there is nothing left to fall back to. Keeps a black-holed earlier address
  /// (no RST, connection just hangs) from stalling a multi-minute HTTP request timeout -- e.g. the
  /// shipped MAUI registration's 600-second timeout -- before ever trying a reachable candidate.
  /// </summary>
  private static readonly TimeSpan PerCandidateConnectTimeout = TimeSpan.FromSeconds(5);

  /// <summary>The delegate assigned to <see cref="SocketsHttpHandler.ConnectCallback"/>. It only owns
  /// the real DNS lookup; every other decision lives in the testable methods below.</summary>
  internal static async ValueTask<Stream> ConnectAsync(
      SocketsHttpConnectionContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    IReadOnlyList<IPAddress> addresses;
    try
    {
      addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken)
          .ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is SocketException or ArgumentException)
    {
      throw new HttpRequestException($"DNS resolution for '{context.DnsEndPoint.Host}' failed.", ex);
    }
    return await ConnectValidatedAsync(
        addresses, context.InitialRequestMessage.RequestUri, context.DnsEndPoint, cancellationToken)
        .ConfigureAwait(false);
  }

  /// <summary>Validates a synthetic resolved-address list and connects a real socket to the first
  /// candidate that accepts, falling back through the rest -- mirroring the multi-address resilience
  /// the default <see cref="SocketsHttpHandler"/> connect logic has (e.g. a dual-stack "localhost"
  /// resolving to both <c>::1</c> and <c>127.0.0.1</c> when only one family is listening). Exercised
  /// directly by tests (e.g. against a loopback <c>TcpListener</c>) without going through real DNS.
  /// </summary>
  internal static async ValueTask<Stream> ConnectValidatedAsync(
      IReadOnlyList<IPAddress> addresses, Uri? requestUri, DnsEndPoint endpoint, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(endpoint);
    var candidates = SelectValidatedAddresses(addresses, requestUri, endpoint.Host);
    SocketException? lastFailure = null;
    for (var i = 0; i < candidates.Count; i++)
    {
      var isLastCandidate = i == candidates.Count - 1;
      var (stream, failure) = await TryConnectAsync(
          candidates[i], endpoint.Port, isLastCandidate ? null : PerCandidateConnectTimeout, cancellationToken)
          .ConfigureAwait(false);
      if (stream is not null) return stream;
      lastFailure = failure;
    }
    throw new HttpRequestException($"Unable to connect to any resolved address for '{endpoint.Host}'.", lastFailure);
  }

  [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transfers to the returned NetworkStream(socket, ownsSocket: true); the catch disposes it if that hand-off never happens.")]
  [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "CA1508's flow analysis assumes SocketException can only escape socket.ConnectAsync below, but the Socket(AddressFamily, SocketType, ProtocolType) constructor is documented to throw SocketException itself (e.g. an unsupported address-family/protocol combination), before `socket` is ever assigned -- so the null-conditional in the SocketException catch guards a real, reachable null.")]
  private static async ValueTask<(Stream? Stream, SocketException? Failure)> TryConnectAsync(
      IPAddress address, int port, TimeSpan? candidateTimeout, CancellationToken cancellationToken)
  {
    using var timeoutCts = candidateTimeout is { } timeout
        ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        : null;
    timeoutCts?.CancelAfter(candidateTimeout!.Value);
    Socket? socket = null;
    try
    {
      // An explicit IPv6 socket is IPv6-only on some platforms (notably Windows) unless DualMode is
      // enabled, so it cannot reach an IPv4-mapped address (e.g. "::ffff:127.0.0.1") -- a shape the
      // endpoint policy explicitly accepts. Map down to the real IPv4 address before choosing the
      // socket's address family instead of opting every candidate into dual-mode.
      var connectAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
      socket = new Socket(connectAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
      await socket.ConnectAsync(connectAddress, port, timeoutCts?.Token ?? cancellationToken).ConfigureAwait(false);
      return (new NetworkStream(socket, ownsSocket: true), null);
    }
    catch (SocketException ex)
    {
      // socket can genuinely be null here: the Socket constructor above is documented to throw
      // SocketException itself (not just ConnectAsync), before any assignment happens.
      socket?.Dispose();
      return (null, ex);
    }
    catch (PlatformNotSupportedException)
    {
      // Socket construction itself can throw when the resolved candidate's address family is
      // unavailable on this device (e.g. IPv6 disabled). Treat it as a per-candidate connect
      // failure -- not a fatal error -- so the fallback loop in ConnectValidatedAsync still tries
      // the remaining candidates instead of aborting outright.
      socket?.Dispose();
      return (null, new SocketException((int)SocketError.AddressFamilyNotSupported));
    }
    catch (OperationCanceledException) when (timeoutCts is not null && !cancellationToken.IsCancellationRequested)
    {
      // Synthesize a real failure instead of (null, null): a run where every candidate times out
      // would otherwise leave the final HttpRequestException with no inner exception, hiding that
      // the cause was a timeout rather than an outright refusal. Unlike the SocketException catch
      // above, socket is genuinely non-null here -- the Socket constructor is synchronous and
      // cannot observe timeoutCts, so only ConnectAsync's await can hit this catch -- but the
      // compiler's nullable-flow analysis can't see that across the throw, hence `!`.
      socket!.Dispose();
      return (null, new SocketException((int)SocketError.TimedOut));
    }
    catch
    {
      socket?.Dispose();
      throw;
    }
  }

  /// <summary>The pure validation gate: for a cleartext request, keeps only resolved addresses that
  /// satisfy <see cref="LocalLLMNetworkPolicy"/> -- refusing the connection outright if none qualify --
  /// before any socket is created. TLS plus the hostname-level policy check already gate the https path,
  /// so all resolved addresses pass through unfiltered there.</summary>
  internal static IReadOnlyList<IPAddress> SelectValidatedAddresses(
      IReadOnlyList<IPAddress> addresses, Uri? requestUri, string host)
  {
    ArgumentNullException.ThrowIfNull(addresses);
    if (addresses.Count == 0)
    {
      throw new HttpRequestException($"DNS resolution for '{host}' returned no usable addresses.");
    }

    // Fail closed: a null request URI must never fall through to "no filtering applied". This
    // helper is internal and testable in isolation, so a future caller passing null (or reusing
    // it outside this client's own ConnectAsync, which always supplies a real request URI) must
    // not silently bypass the private-address gate.
    if (requestUri is null)
    {
      throw new HttpRequestException($"Refusing a local-model connection to '{host}': no request URI is available to validate.");
    }
    if (requestUri.Scheme != Uri.UriSchemeHttp)
    {
      return addresses; // TLS plus the hostname-level policy check gate the https path
    }

    var privateAddresses = addresses.Where(LocalLLMNetworkPolicy.IsPrivateNetworkHost).ToArray();
    if (privateAddresses.Length == 0)
    {
      throw new HttpRequestException(
          $"Refusing a cleartext local-model connection to '{host}': no resolved address is a private/local network address.");
    }
    return privateAddresses;
  }
}
