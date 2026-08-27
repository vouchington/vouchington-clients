import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    @ViewBuilder
    var notificationSettingsContent: some View {
        if notificationSettingsViewModel.current == nil {
            switch notificationSettingsViewModel.state {
            case .idle, .loading:
                LoadingView()
            case let .error(error):
                ErrorStateView(error: error) { await notificationSettingsViewModel.load() }
            case .loaded:
                EmptyStateView(
                    icon: "bell",
                    title: .message(.nativeSwiftEmptyStateUnableToLoad)
                )
            }
        } else {
            Group {
                if case let .error(error) = notificationSettingsViewModel.state {
                    notificationSettingsRefreshError(error)
                }
                if let statusMessage = notificationSettingsViewModel.statusMessage {
                    Text(verbatim: UiMessages.string(statusMessage, locale: nativeUiLocale))
                        .font(Typography.subheadline)
                        .foregroundStyle(Colors.negativeVote)
                }
                notificationSettingsControls
            }
        }
    }

    private func notificationSettingsRefreshError(_ error: any LocalizedError) -> some View {
        HStack(alignment: .top, spacing: Spacing.sm) {
            Image(systemName: "exclamationmark.triangle")
                .foregroundStyle(Colors.negativeVote)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeCommonSomethingWentWrong, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                if let description = error.errorDescription {
                    Text(description)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
            Spacer()
            Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                Task { await notificationSettingsViewModel.load() }
            }
            .buttonStyle(.bordered)
        }
    }
}
