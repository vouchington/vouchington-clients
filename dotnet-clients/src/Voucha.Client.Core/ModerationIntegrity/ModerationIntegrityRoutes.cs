namespace Voucha.Client.Core.ModerationIntegrity;

public enum ModerationIntegrityRouteKind
{
  ReportFlags,
  ReportPenalties,
  VoteFlags,
  VotePenalties,
}

public static class ModerationIntegrityRoutes
{
  public static bool TryResolve(string? path, out ModerationIntegrityRouteKind kind)
  {
    kind = path switch
    {
      "/report-integrity/flags" => ModerationIntegrityRouteKind.ReportFlags,
      "/report-integrity/penalties" => ModerationIntegrityRouteKind.ReportPenalties,
      "/vote-integrity/flags" => ModerationIntegrityRouteKind.VoteFlags,
      "/vote-integrity/penalties" => ModerationIntegrityRouteKind.VotePenalties,
      _ => default,
    };
    return path is "/report-integrity/flags" or "/report-integrity/penalties" or
        "/vote-integrity/flags" or "/vote-integrity/penalties";
  }
}
