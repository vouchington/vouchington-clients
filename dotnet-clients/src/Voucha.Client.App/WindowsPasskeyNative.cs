using System.Runtime.InteropServices;

namespace Voucha.Client.App;

// Layouts through assertion-options version 3 / assertion version 1, from Microsoft webauthn.h.
internal static class WindowsPasskeyNative
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct ClientData
  {
    internal uint Version, Length;
    internal nint Data, HashAlgorithm;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Credential
  {
    internal uint Version, Length;
    internal nint Id, Type;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct BufferList
  {
    internal uint Count;
    internal nint Items;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Options
  {
    internal uint Version, Timeout;
    internal BufferList Credentials, Extensions;
    internal uint Attachment, UserVerification, Flags;
    internal nint AppId, UsedAppId, CancellationId;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Assertion
  {
    internal uint Version, AuthenticatorDataLength;
    internal nint AuthenticatorData;
    internal uint SignatureLength;
    internal nint Signature;
    internal Credential Credential;
    internal uint UserIdLength;
    internal nint UserId;
  }

  [DllImport("webauthn.dll", ExactSpelling = true)]
  internal static extern uint WebAuthNGetApiVersionNumber();

  [DllImport("webauthn.dll", ExactSpelling = true)]
  internal static extern int WebAuthNGetCancellationId(out Guid cancellationId);

  [DllImport("webauthn.dll", ExactSpelling = true)]
  internal static extern int WebAuthNCancelCurrentOperation(ref Guid cancellationId);

  [DllImport("webauthn.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
  internal static extern int WebAuthNAuthenticatorGetAssertion(nint window, string rpId,
      ref ClientData clientData, ref Options options, out nint assertion);

  [DllImport("webauthn.dll", ExactSpelling = true)]
  internal static extern void WebAuthNFreeAssertion(nint assertion);
}
