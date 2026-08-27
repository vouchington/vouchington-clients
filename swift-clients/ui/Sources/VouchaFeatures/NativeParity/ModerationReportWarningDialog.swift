import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct ModerationReportWarningDialog: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let reportId: String
    @Bindable
    var viewModel: ModerationReportsViewModel
    @Binding
    var isPresented: Bool
    @State
    private var reason = ""
    @State
    private var publicMessage = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Text(UiMessages.string(.nativeSwiftModerationReportsIssueWarning, locale: nativeUiLocale))
                .font(Typography.headline)
            TextField(
                UiMessages.string(.nativeSwiftModerationReportsReasonField, locale: nativeUiLocale),
                text: $reason,
                axis: .vertical
            )
            .textFieldStyle(.roundedBorder)
            Text(characterLimit(count: reason.utf16.count, limit: AdminWarningLimits.reasonUTF16))
                .font(Typography.caption)
                .foregroundStyle(.secondary)
            TextField(
                UiMessages.string(.nativeSwiftModerationReportsPublicMessageOptional, locale: nativeUiLocale),
                text: $publicMessage,
                axis: .vertical
            )
            .textFieldStyle(.roundedBorder)
            Text(characterLimit(
                count: publicMessage.utf16.count,
                limit: AdminWarningLimits.publicMessageUTF16
            ))
            .font(Typography.caption)
            .foregroundStyle(.secondary)
            HStack {
                Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                    isPresented = false
                }
                Button(UiMessages.string(.nativeSwiftModerationReportsIssueWarning, locale: nativeUiLocale)) {
                    let action = ModerationReportAction.warn(
                        reason: reason.trimmingCharacters(in: .whitespacesAndNewlines),
                        publicMessage: publicMessage
                    )
                    isPresented = false
                    Task { await viewModel.perform(action, reportId: reportId) }
                }
                .buttonStyle(.borderedProminent)
                .disabled(reason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    || reason.utf16.count > AdminWarningLimits.reasonUTF16
                    || publicMessage.utf16.count > AdminWarningLimits.publicMessageUTF16
                    || viewModel.isQueueActionInProgress)
            }
        }
        .padding(Spacing.lg)
        .frame(minWidth: 420)
    }

    private func characterLimit(count: Int, limit: Int) -> String {
        UiMessages.string(
            .nativeSwiftModerationReportsCharacterLimit,
            parameters: [
                "count": UiMessages.number(count, locale: nativeUiLocale),
                "limit": UiMessages.number(limit, locale: nativeUiLocale)
            ],
            locale: nativeUiLocale
        )
    }
}
