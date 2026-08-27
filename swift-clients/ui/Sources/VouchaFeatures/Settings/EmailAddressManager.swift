import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

public struct EmailAddressManager: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: EmailAddressManagerViewModel
    private let recoveryMode: Bool
    private let onVerified: () -> Void

    public init(client: APIClient, recoveryMode: Bool = false, onVerified: @escaping () -> Void = {}) {
        _viewModel = State(initialValue: EmailAddressManagerViewModel(client: client))
        self.recoveryMode = recoveryMode
        self.onVerified = onVerified
    }

    public var body: some View {
        @Bindable
        var viewModel = viewModel
        VStack(alignment: .leading, spacing: Spacing.md) {
            if recoveryMode {
                Text(UiMessages.string(.nativeSwiftSettingsVerifyAnEmailAddress, locale: nativeUiLocale))
                    .font(Typography.headline)
                Text(UiMessages.string(
                    .nativeSwiftSettingsThisActionRequiresAverifiedEmailAddress,
                    locale: nativeUiLocale
                ))
                .foregroundStyle(Colors.secondaryLabel)
            }

            if viewModel.emailAddresses.isEmpty, viewModel.phase == .enterEmail {
                Text(UiMessages.string(.nativeSwiftSettingsNoEmailAddressesYet, locale: nativeUiLocale))
                    .foregroundStyle(Colors.secondaryLabel)
            } else if !viewModel.emailAddresses.isEmpty {
                ForEach(viewModel.emailAddresses, id: \.id) { address in
                    HStack {
                        Text(address.emailAddress)
                        Spacer()
                        if address.isPrimary {
                            Text(UiMessages.string(.nativeSwiftSettingsPrimary, locale: nativeUiLocale))
                                .foregroundStyle(Colors.secondaryLabel)
                        }
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.pagination.hasMore,
                    isLoading: viewModel.pagination.isLoading,
                    hasError: viewModel.pagination.lastError != nil,
                    accessibilityIdentifier: "email-addresses-pagination"
                ) {
                    await viewModel.loadMore()
                }
            }

            switch viewModel.phase {
            case .enterEmail:
                TextField(
                    UiMessages.string(.nativeSwiftSettingsEmailAddress, locale: nativeUiLocale),
                    text: $viewModel.emailAddress
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftSettingsSendVerificationCode, locale: nativeUiLocale)) {
                    Task { await viewModel.requestVerification() }
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.emailAddress.trimmingCharacters(in: .whitespacesAndNewlines)
                    .isEmpty || viewModel.isLoading)
            case .enterCode:
                Text(viewModel.emailAddress).font(Typography.subheadline)
                TextField(
                    UiMessages.string(.nativeSwiftSettingsMessage8CharacterCode, locale: nativeUiLocale),
                    text: $viewModel.verificationCode
                )
                .textFieldStyle(.roundedBorder)
                HStack {
                    Button(UiMessages.string(.nativeSwiftCommonVerify, locale: nativeUiLocale)) {
                        Task { await viewModel.verify() }
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(viewModel.verificationCode.count != 8 || viewModel.isLoading)
                    Button(UiMessages.string(.nativeSwiftSettingsUseAnotherAddress, locale: nativeUiLocale)) {
                        viewModel.startAnotherAddress()
                    }
                    .buttonStyle(.bordered)
                }
            case .verified:
                Button(UiMessages.string(.nativeSwiftCommonDone, locale: nativeUiLocale)) { onVerified() }
                    .buttonStyle(.borderedProminent)
                if !recoveryMode {
                    Button(UiMessages.string(.nativeSwiftSettingsAddAnotherAddress, locale: nativeUiLocale)) {
                        viewModel.startAnotherAddress()
                    }.buttonStyle(.bordered)
                }
            }

            if let message = viewModel.statusMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale)).foregroundStyle(Colors.secondaryLabel)
            }
            if let error = viewModel.errorMessage {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
            }
        }
        .task { await viewModel.load() }
    }
}
