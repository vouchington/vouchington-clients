using System.Runtime.InteropServices;
using Voucha.Client.Core.Auth;
using Voucha.Client.App;
using Xunit;
using static Voucha.Client.App.WindowsPasskeyNative;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class WindowsPasskeyAssertionTests
{
  private static PasskeyAssertionRequest Request(string? verification = "preferred") => PasskeyAssertionRequest.Create(
      new("AQID", "voucha.ai", verification, [new("BAU")]), new Uri("https://voucha.ai"));

  [Fact]
  public void NativeCallReceivesChallengeAllowlistVerificationWindowAndCancellation()
  {
    using var native = new FakeApi();
    var result = WindowsPasskeyAssertion.Get(Request(), 42, CancellationToken.None, native);
    Assert.Equal("BAU", result.Id);
    Assert.Equal("Bg", result.Response.AuthenticatorData);
    Assert.Equal("Bw", result.Response.Signature);
    Assert.Equal("", result.Response.UserHandle);
    Assert.True(native.Freed);
    Assert.Equal(1, native.Calls);
  }

  [Fact]
  public void PreCanceledRequestNeverInvokesWindows()
  {
    using var native = new FakeApi();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    Assert.Throws<OperationCanceledException>(() => WindowsPasskeyAssertion.Get(Request(), 42, cancellation.Token, native));
    Assert.Equal(0, native.Calls);
  }

  [Fact]
  public void CancellationDismissesNativeOperationAndDiscardsEvenSuccessfulAssertion()
  {
    using var cancellation = new CancellationTokenSource();
    using var native = new FakeApi { DuringCall = cancellation.Cancel };
    var error = Assert.Throws<OperationCanceledException>(() =>
        WindowsPasskeyAssertion.Get(Request(), 42, cancellation.Token, native));
    Assert.Equal(cancellation.Token, error.CancellationToken);
    Assert.True(native.Canceled);
    Assert.True(native.Freed);
  }

  [Theory]
  [InlineData(0x800704C7u, true)]
  [InlineData(0x80090036u, true)]
  [InlineData(0x800705B4u, false)]
  [InlineData(0x80090011u, false)]
  [InlineData(0x80090035u, false)]
  [InlineData(0x80004005u, false)]
  public void NativeFailuresNeverProduceAssertionAndAlwaysReleaseIt(uint result, bool canceled)
  {
    using var native = new FakeApi { Result = unchecked((int)result) };
    var error = Record.Exception(() => WindowsPasskeyAssertion.Get(Request(), 42, CancellationToken.None, native));
    if (canceled) Assert.IsType<OperationCanceledException>(error);
    else Assert.IsType<InvalidOperationException>(error);
    Assert.True(native.Freed);
  }

  [Fact]
  public void UnsupportedWindowsFailsBeforePrompt()
  {
    using var native = new FakeApi { ApiVersion = 0 };
    Assert.Throws<PlatformNotSupportedException>(() => WindowsPasskeyAssertion.Get(Request(), 42, CancellationToken.None, native));
    Assert.Equal(0, native.Calls);
  }

  [Fact]
  public void InvalidNativeBufferIsRejectedAndReleased()
  {
    using var native = new FakeApi { InvalidBuffer = true };
    Assert.Throws<InvalidOperationException>(() => WindowsPasskeyAssertion.Get(Request(), 42, CancellationToken.None, native));
    Assert.True(native.Freed);
  }

  [Theory]
  [InlineData("required", 1u)]
  [InlineData("preferred", 2u)]
  [InlineData("discouraged", 3u)]
  [InlineData(null, 2u)]
  public void NativeVerificationFollowsServerOptions(string? verification, uint expected)
  {
    using var native = new FakeApi { ExpectedVerification = expected };
    var response = WindowsPasskeyAssertion.Get(Request(verification), 42, CancellationToken.None, native);
    Assert.Equal("BAU", response.Id);
  }

  private sealed class FakeApi : IWindowsPasskeyApi, IDisposable
  {
    private readonly List<nint> allocations = [];
    private readonly Guid cancellationId = new("f17bca39-2e14-4e24-b220-af0f38736d87");
    public uint ApiVersion { get; init; } = 1;
    public uint ExpectedVerification { get; init; } = 2;
    public int Result { get; init; }
    public bool InvalidBuffer { get; init; }
    public Action? DuringCall { get; init; }
    public int Calls { get; private set; }
    public bool Freed { get; private set; }
    public bool Canceled { get; private set; }
    public uint Version() => ApiVersion;
    public int NewCancellationId(out Guid id) { id = cancellationId; return 0; }
    public int CancelOperation(ref Guid id)
    {
      Assert.Equal(cancellationId, id);
      Canceled = true;
      return 0;
    }
    public int GetAssertion(nint window, string rpId, ref ClientData data, ref Options options, out nint assertion)
    {
      Calls++;
      Assert.Equal((nint)42, window);
      Assert.Equal("voucha.ai", rpId);
      Assert.Equal(3u, options.Version);
      Assert.Equal(60_000u, options.Timeout);
      Assert.Equal(ExpectedVerification, options.UserVerification);
      Assert.Equal(0u, options.Attachment);
      Assert.Equal(cancellationId, Marshal.PtrToStructure<Guid>(options.CancellationId));
      Assert.Equal("SHA-256", Marshal.PtrToStringUni(data.HashAlgorithm));
      var json = new byte[data.Length];
      Marshal.Copy(data.Data, json, 0, json.Length);
      Assert.Equal(Request().ClientDataJson.ToArray(), json);
      Assert.Equal(1u, options.Credentials.Count);
      var credential = Marshal.PtrToStructure<Credential>(options.Credentials.Items);
      Assert.Equal(2u, credential.Length);
      Assert.Equal("public-key", Marshal.PtrToStringUni(credential.Type));
      Assert.Equal(4, Marshal.ReadByte(credential.Id));
      Assert.Equal(5, Marshal.ReadByte(credential.Id, 1));
      var value = new Assertion
      {
        Version = 1,
        Credential = new Credential { Version = 1, Length = 2, Id = Bytes([4, 5]) },
        AuthenticatorDataLength = InvalidBuffer ? uint.MaxValue : 1,
        AuthenticatorData = Bytes([6]),
        SignatureLength = 1,
        Signature = Bytes([7]),
      };
      assertion = Marshal.AllocHGlobal(Marshal.SizeOf<Assertion>());
      allocations.Add(assertion);
      Marshal.StructureToPtr(value, assertion, false);
      DuringCall?.Invoke();
      return Result;
    }
    public void FreeAssertion(nint assertion) { Assert.Contains(assertion, allocations); Freed = true; }
    private nint Bytes(byte[] bytes)
    {
      var pointer = Marshal.AllocHGlobal(bytes.Length);
      allocations.Add(pointer);
      Marshal.Copy(bytes, 0, pointer, bytes.Length);
      return pointer;
    }
    public void Dispose() { foreach (var pointer in allocations) Marshal.FreeHGlobal(pointer); }
  }
}
