using static Voucha.Client.App.WindowsPasskeyNative;

namespace Voucha.Client.App;

internal interface IWindowsPasskeyApi
{
  uint Version();
  int NewCancellationId(out Guid id);
  int CancelOperation(ref Guid id);
  int GetAssertion(nint window, string rpId, ref ClientData data, ref Options options, out nint assertion);
  void FreeAssertion(nint assertion);
}

internal sealed class WindowsPasskeyApi : IWindowsPasskeyApi
{
  public uint Version() => WebAuthNGetApiVersionNumber();
  public int NewCancellationId(out Guid id) => WebAuthNGetCancellationId(out id);
  public int CancelOperation(ref Guid id) => WebAuthNCancelCurrentOperation(ref id);
  public int GetAssertion(nint window, string rpId, ref ClientData data, ref Options options, out nint assertion) =>
      WebAuthNAuthenticatorGetAssertion(window, rpId, ref data, ref options, out assertion);
  public void FreeAssertion(nint assertion) => WebAuthNFreeAssertion(assertion);
}
