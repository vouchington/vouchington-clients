import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    var sessionsSection: some View {
        section(.nativeSwiftSettingsSessions, systemImage: "clock") {
            if viewModel.sessions.isEmpty {
                Text(UiMessages.string(.nativeSwiftSettingsNoActiveSessions, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
            } else {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.sessions) { session in
                        HStack(alignment: .top, spacing: Spacing.md) {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(
                                    session.deviceName
                                        ?? UiMessages.string(
                                            .nativeSwiftSettingsUnnamedDevice,
                                            locale: nativeUiLocale
                                        )
                                )
                                if let userAgent = session.userAgent {
                                    Text(userAgent)
                                        .font(Typography.caption)
                                        .foregroundStyle(Colors.secondaryLabel)
                                }
                                if let ipAddress = session.ipAddress {
                                    Text(ipAddress)
                                        .font(Typography.caption)
                                        .foregroundStyle(Colors.secondaryLabel)
                                }
                                if session.isCurrent {
                                    Text(UiMessages.string(.nativeSwiftSettingsCurrentSession, locale: nativeUiLocale))
                                        .font(Typography.caption)
                                        .foregroundStyle(Colors.secondaryLabel)
                                }
                            }
                            Spacer(minLength: 0)
                            Button(
                                UiMessages.string(
                                    session.isCurrent
                                        ? .nativeSwiftProfileSignOut
                                        : .nativeSwiftSettingsRevoke,
                                    locale: nativeUiLocale
                                ),
                                role: .destructive
                            ) {
                                pendingSessionRevocation = session
                            }
                            .buttonStyle(.bordered)
                            .disabled(viewModel.isLoading)
                        }
                    }
                    HybridPaginationControl(
                        hasMore: viewModel.sessionPagination.hasMore,
                        isLoading: viewModel.sessionPagination.isLoading,
                        hasError: viewModel.sessionPagination.lastError != nil,
                        accessibilityIdentifier: "sessions-pagination"
                    ) {
                        await viewModel.loadMoreSessions()
                    }
                }

                Button(
                    UiMessages.string(.nativeSwiftSettingsRevokeAllSessions, locale: nativeUiLocale),
                    role: .destructive
                ) {
                    confirmingAllSessionRevocation = true
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isLoading)
            }
        }
    }
}
