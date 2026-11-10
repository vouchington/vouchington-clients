using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed class ReportJudgementRunEndedException : Exception
{
  public ReportJudgementRunEndedException()
      : this(ModerationRunStatus.Failed)
  {
  }

  public ReportJudgementRunEndedException(string message)
      : base(message)
  {
  }

  public ReportJudgementRunEndedException(string message, Exception innerException)
      : base(message, innerException)
  {
  }

  public ReportJudgementRunEndedException(ModerationRunStatus status)
      : base($"Report judgement run ended with status {status}.")
  {
    Status = status;
  }

  public ModerationRunStatus Status { get; } = ModerationRunStatus.Failed;

  public UiMessageKey Key => Status == ModerationRunStatus.Expired
      ? UiMessageKey.NativeSwiftModerationReportsAiJudgementRunExpired
      : UiMessageKey.NativeSwiftModerationReportsAiJudgementRunFailed;
}
