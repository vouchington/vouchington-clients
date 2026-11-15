using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class ReportIntegrityPenaltiesPage(
    ReportIntegrityPenaltyViewModel viewModel, ReportIntegrityPenaltyReviewViewModel? reviews = null)
    : IntegrityPenaltiesPageBase<ReportAbusePenalty>(
        viewModel, UiMessageKey.NativeSwiftIntegrityReportPenaltiesTitle,
        "report", "/report-integrity/flags", "/report-integrity/penalties",
        reviews is null ? null : new IntegrityPenaltyReviewSection(reviews, "report"))
{
  protected override IReadOnlyList<IntegrityPenaltyRow> PresentedRows => viewModel.Rows;
}
