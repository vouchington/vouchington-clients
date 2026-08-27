import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct IdentityVerificationAttemptGrantSurface: View {
    @Environment(\.locale)
    private var locale
    @State
    private var viewModel: IdentityVerificationGrantViewModel

    init(client: APIClient?, idOrUsername: String, isAdministrator: Bool) {
        self.init(
            viewModel: IdentityVerificationGrantViewModel(
                client: client,
                idOrUsername: idOrUsername,
                isAdministrator: isAdministrator
            )
        )
    }

    init(viewModel: IdentityVerificationGrantViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        @Bindable
        var viewModel = viewModel
        if !viewModel.isAdministrator {
            EmptyStateView(
                icon: "lock.shield",
                title: .message(.nativeSwiftIdentityVerificationAdministratorRequired),
                message: .message(.nativeSwiftIdentityVerificationAdministratorMessage)
            )
        } else {
            Form {
                Section {
                    Text(verbatim: localized(.nativeSwiftIdentityVerificationDescription))
                } header: {
                    Text(verbatim: localized(.nativeSwiftIdentityVerificationTitle))
                }
                Section {
                    TextField(localized(.nativeSwiftIdentityVerificationNote), text: $viewModel.note, axis: .vertical)
                        .lineLimit(3 ... 8)
                        .accessibilityIdentifier("identity-verification-grant-note")
                    Button(localized(
                        viewModel.isSubmitting
                            ? .nativeSwiftIdentityVerificationGranting
                            : .nativeSwiftIdentityVerificationGrant
                    )) {
                        Task { await viewModel.grant() }
                    }
                    .disabled(!viewModel.canSubmit)
                    .accessibilityIdentifier("identity-verification-grant-submit")
                    if let error = viewModel.loadError {
                        Text(verbatim: UiMessages.string(error, locale: locale))
                    }
                    if let message = viewModel.submissionMessage {
                        Text(verbatim: UiMessages.string(message, locale: locale))
                    }
                }
            }
            .task { await viewModel.load() }
            .navigationTitle(localized(.nativeSwiftIdentityVerificationTitle))
        }
    }

    private func localized(_ key: UiMessageKey) -> String {
        UiMessages.string(.message(key), locale: locale)
    }
}
