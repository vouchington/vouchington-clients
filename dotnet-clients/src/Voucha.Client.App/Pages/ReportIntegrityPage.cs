using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;

namespace Voucha.Client.App.Pages;

public sealed partial class ReportIntegrityPage
    : IntegrityFlagQueuePageBase<ReportIntegrityViewModel>
{
  public ReportIntegrityPage(ReportIntegrityViewModel viewModel)
      : base(
          viewModel,
          UiMessageKey.NativeDotnetModerationReportIntegrity,
          "/report-integrity/flags",
          "/report-integrity/penalties",
          "report-integrity")
  {
    InitializeRender(Render);
  }
}
