import SwiftUI
import VouchaLocalization
import VouchaModels

struct ModerationReportActionButtons: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let report: StaffModerationReport
    @Bindable
    var viewModel: ModerationReportsViewModel
    let onWarning: () -> Void
    let onConfirm: (ModerationReportsConfirmation) -> Void

    var body: some View {
        HStack {
            if viewModel.actionIsAllowed(.review, for: report) {
                Button(UiMessages.string(.nativeSwiftModerationReportsReview, locale: nativeUiLocale)) {
                    onConfirm(.report(.review, report.id))
                }
            }
            if viewModel.actionIsAllowed(.dismiss, for: report) {
                Button(UiMessages.string(.nativeSwiftModerationReportsDismiss, locale: nativeUiLocale)) {
                    onConfirm(.report(.dismiss, report.id))
                }
            }
            Button(UiMessages.string(.nativeSwiftModerationReportsRerunJudgement, locale: nativeUiLocale)) {
                Task { await viewModel.perform(.rerunJudgement, reportId: report.id) }
            }
            if viewModel.actionIsAllowed(.warn(reason: "warning", publicMessage: nil), for: report) {
                Button(
                    UiMessages.string(.nativeSwiftModerationReportsWarnUser, locale: nativeUiLocale),
                    action: onWarning
                )
            }
            if viewModel.actionIsAllowed(.confirmBanEvasion, for: report) {
                Button(UiMessages.string(
                    .nativeSwiftModerationReportsConfirmBanEvasion,
                    locale: nativeUiLocale
                ), role: .destructive) {
                    onConfirm(.report(.confirmBanEvasion, report.id))
                }
                Button(UiMessages.string(
                    .nativeSwiftModerationReportsDismissBanEvasion,
                    locale: nativeUiLocale
                )) {
                    onConfirm(.report(.dismissBanEvasion, report.id))
                }
            }
            if viewModel.actionIsAllowed(.removeTarget, for: report) {
                Button(UiMessages.string(
                    .nativeSwiftModerationReportsRemoveContent,
                    locale: nativeUiLocale
                ), role: .destructive) {
                    onConfirm(.report(.removeTarget, report.id))
                }
            }
        }
        .disabled(viewModel.isQueueActionInProgress || viewModel.inFlightReportIds.contains(report.id))
        .opacity(viewModel.isQueueActionInProgress || viewModel.inFlightReportIds.contains(report.id) ? 0.6 : 1)
    }
}
