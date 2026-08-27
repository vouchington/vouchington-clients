using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Fediverse;

public enum FediverseTrustTier
{
  Unrated,
  Distrusted,
  Neutral,
  Trusted,
}

public static class FediverseTrust
{
  public static FediverseTrustTier FromElection(HostnameElection? election) => election switch
  {
    null => FediverseTrustTier.Unrated,
    { VotesCountUp: 0, VotesCountDown: 0 } => FediverseTrustTier.Unrated,
    { VotesScoreNet: >= 3, VotesCountUp: >= 5 } => FediverseTrustTier.Trusted,
    { VotesScoreNet: <= -3 } => FediverseTrustTier.Distrusted,
    _ => FediverseTrustTier.Neutral,
  };

  public static UiMessageKey MessageKey(FediverseTrustTier tier) => tier switch
  {
    FediverseTrustTier.Trusted => UiMessageKey.NativeSwiftRouteSurfaceTrusted,
    FediverseTrustTier.Distrusted => UiMessageKey.NativeSwiftRouteSurfaceDistrusted,
    FediverseTrustTier.Neutral => UiMessageKey.NativeSwiftRouteSurfaceNeutral,
    _ => UiMessageKey.NativeSwiftRouteSurfaceUnrated,
  };
}
