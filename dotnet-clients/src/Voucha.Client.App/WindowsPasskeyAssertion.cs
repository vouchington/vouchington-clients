using System.Runtime.InteropServices;
using Voucha.Client.Core.Auth;
using static Voucha.Client.App.WindowsPasskeyNative;

namespace Voucha.Client.App;

internal static class WindowsPasskeyAssertion
{
  internal static PasskeyAssertionResponse Get(PasskeyAssertionRequest request, nint window, CancellationToken token,
      IWindowsPasskeyApi? native = null)
  {
    token.ThrowIfCancellationRequested();
    native ??= new WindowsPasskeyApi();
    if (native.Version() < 1)
    {
      throw new PlatformNotSupportedException("This Windows version does not support cancellable WebAuthn sign-in.");
    }

    Marshal.ThrowExceptionForHR(native.NewCancellationId(out var cancellationId));
    using var memory = new NativeMemory();
    var clientData = new ClientData
    {
      Version = 1,
      Length = checked((uint)request.ClientDataJson.Length),
      Data = memory.Bytes(request.ClientDataJson.ToArray()),
      HashAlgorithm = memory.Text("SHA-256"),
    };
    var credentials = memory.Allocate(checked(Marshal.SizeOf<Credential>() * request.CredentialIds.Count));
    for (var index = 0; index < request.CredentialIds.Count; index++)
    {
      var id = request.CredentialIds[index];
      Marshal.StructureToPtr(new Credential
      {
        Version = 1,
        Length = checked((uint)id.Length),
        Id = memory.Bytes(id.ToArray()),
        Type = memory.Text("public-key"),
      }, credentials + index * Marshal.SizeOf<Credential>(), false);
    }

    var cancellation = memory.Allocate(Marshal.SizeOf<Guid>());
    Marshal.StructureToPtr(cancellationId, cancellation, false);
    var options = new Options
    {
      Version = 3,
      Timeout = 60_000,
      Credentials = new BufferList { Count = checked((uint)request.CredentialIds.Count), Items = credentials },
      // Any attachment allows Windows Hello, roaming security keys, and OS-supported passkey providers.
      UserVerification = 1,
      CancellationId = cancellation,
    };
    using var registration = token.Register(() => _ = native.CancelOperation(ref cancellationId));
    token.ThrowIfCancellationRequested();
    nint assertion = 0;
    try
    {
      var result = native.GetAssertion(window, request.RpId, ref clientData, ref options, out assertion);
      token.ThrowIfCancellationRequested();
      ThrowForResult(result);
      if (assertion == 0) throw new InvalidOperationException("Windows returned no passkey assertion.");
      var value = Marshal.PtrToStructure<Assertion>(assertion);
      return request.Complete(Read(value.Credential.Id, value.Credential.Length),
          Read(value.AuthenticatorData, value.AuthenticatorDataLength),
          Read(value.Signature, value.SignatureLength), Read(value.UserId, value.UserIdLength));
    }
    finally
    {
      if (assertion != 0) native.FreeAssertion(assertion);
    }
  }

  private static byte[] Read(nint pointer, uint length)
  {
    if (length > 1_048_576 || (length > 0 && pointer == 0))
      throw new InvalidOperationException("Windows returned invalid passkey data.");
    var bytes = new byte[checked((int)length)];
    if (bytes.Length > 0) Marshal.Copy(pointer, bytes, 0, bytes.Length);
    return bytes;
  }

  private static void ThrowForResult(int result)
  {
    switch (unchecked((uint)result))
    {
      case 0: return;
      case 0x800704C7: // HRESULT_FROM_WIN32(ERROR_CANCELLED)
      case 0x80090036: // NTE_USER_CANCELLED
        throw new OperationCanceledException("Passkey sign-in was canceled.");
      case 0x800705B4: // HRESULT_FROM_WIN32(ERROR_TIMEOUT)
      case 0x80090022: // NTE_SILENT_CONTEXT (UI/interaction unavailable)
        throw new InvalidOperationException("Windows could not complete passkey sign-in. Try again with an available authenticator.");
      case 0x80090011: // NTE_NOT_FOUND
      case 0x80090035: // NTE_DEVICE_NOT_FOUND
        throw new InvalidOperationException("No matching passkey is available. Set up Windows Hello or connect your security key.");
      default:
        throw new InvalidOperationException("Windows passkey sign-in failed.", Marshal.GetExceptionForHR(result));
    }
  }

  private sealed class NativeMemory : IDisposable
  {
    private readonly List<nint> allocations = [];
    internal nint Allocate(int length)
    {
      var pointer = Marshal.AllocHGlobal(Math.Max(length, 1));
      allocations.Add(pointer);
      return pointer;
    }
    internal nint Bytes(byte[] bytes)
    {
      var pointer = Allocate(bytes.Length);
      Marshal.Copy(bytes, 0, pointer, bytes.Length);
      return pointer;
    }
    internal nint Text(string value)
    {
      var pointer = Marshal.StringToHGlobalUni(value);
      allocations.Add(pointer);
      return pointer;
    }
    public void Dispose()
    {
      foreach (var pointer in allocations) Marshal.FreeHGlobal(pointer);
    }
  }
}
