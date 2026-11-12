import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeProfileReportReason: Identifiable {
    let title: UiMessage
    let value: String

    var id: String {
        value
    }
}

let nativeProfileReportReasons = [
    NativeProfileReportReason(title: UiMessage(.nativeSwiftCommentThreadHarassment), value: "harassment"),
    NativeProfileReportReason(title: UiMessage(.nativeSwiftCommentThreadIllegalContent), value: "illegal_content"),
    NativeProfileReportReason(title: UiMessage(.nativeSwiftCommentThreadMisinformation), value: "misinformation"),
    NativeProfileReportReason(title: UiMessage(.nativeSwiftCommentThreadOther), value: "other"),
    NativeProfileReportReason(title: UiMessage(.nativeSwiftCommentThreadSpam), value: "spam")
]

extension NativeDetailSurface {
    @ViewBuilder
    var reportingControls: some View {
        if isSignedIn, viewModel.detailReportTarget != nil {
            HStack {
                Button {
                    showingReportDialog = true
                } label: {
                    Label(reportButtonTitle, systemImage: "flag")
                        .font(Typography.caption)
                }
                .buttonStyle(.bordered)
                .tint(.secondary)
                .disabled(viewModel.detailReportSubmissionState != .idle)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Spacing.md)
            .padding(.vertical, Spacing.sm)
            .background(Colors.background.opacity(0.85))
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
    }

    var reportSuccessTitle: String {
        UiMessages.string(.nativeSwiftModerationReportsReportSubmitted, locale: nativeUiLocale)
    }

    var reportSuccessMessage: String {
        UiMessages.string(.nativeSwiftModerationReportsReportSubmittedMessage, locale: nativeUiLocale)
    }

    var reportButtonTitle: String {
        switch viewModel.detailReportSubmissionState {
        case .idle: UiMessages.string(.nativeSwiftModerationReportsReport, locale: nativeUiLocale)
        case .submitting: UiMessages.string(.nativeSwiftModerationReportsReporting, locale: nativeUiLocale)
        case .submitted: UiMessages.string(.nativeSwiftModerationReportsReported, locale: nativeUiLocale)
        }
    }

    var reportDialogTitle: String {
        let subjectName = viewModel.detailReportTarget?.subjectName
            ?? UiMessages.string(.nativeSwiftModerationReportsItem, locale: nativeUiLocale)
        return UiMessages.string(
            .nativeSwiftModerationReportsReportSubject,
            parameters: ["subject": subjectName],
            locale: nativeUiLocale
        )
    }

    var reportDialogMessage: String {
        let subjectName = viewModel.detailReportTarget?.subjectName
            ?? UiMessages.string(.nativeSwiftModerationReportsItem, locale: nativeUiLocale)
        return UiMessages.string(
            .nativeSwiftModerationReportsReportDialogMessage,
            parameters: ["subject": subjectName],
            locale: nativeUiLocale
        )
    }

    func startReportDetail(reason: String) {
        guard viewModel.detailReportTarget != nil,
              viewModel.detailReportSubmissionState == .idle else { return }
        viewModel.detailPendingReportReason = reason
        viewModel.detailPendingReportNote = ""
        showingReportNoteDialog = true
    }

    func handleReportTurnstileToken(_ token: String) {
        viewModel.detailShowingReportTurnstile = false
        if let reason = viewModel.detailPendingReportReason {
            Task {
                let submitted = await reportDetail(
                    reason: reason,
                    note: viewModel.detailPendingReportNote.trimmedOrNil,
                    turnstileToken: token
                )
                if submitted {
                    viewModel.clearPendingReportDraft()
                }
            }
        }
    }

    func reportDetail(reason: String, note: String? = nil, turnstileToken: String) async -> Bool {
        guard let target = viewModel.detailReportTarget else { return false }
        return await viewModel.report(
            target: target,
            reason: reason,
            note: note,
            turnstileToken: turnstileToken
        )
    }

    func retryReportDetail() {
        viewModel.detailReportErrorMessage = nil
        guard viewModel.detailPendingReportReason != nil else { return }
        viewModel.detailShowingReportTurnstile = true
    }

    func cancelReportDetail() {
        viewModel.detailReportErrorMessage = nil
        viewModel.clearPendingReportDraft()
    }
}
