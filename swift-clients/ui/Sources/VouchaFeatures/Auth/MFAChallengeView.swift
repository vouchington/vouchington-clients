import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

/// Prompts the user to enter their TOTP authenticator code.
public struct MFAChallengeView: View {
    @Bindable
    public var viewModel: MFAChallengeViewModel
    @Environment(\.dismiss)
    private var dismiss
    @Environment(\.locale)
    private var locale
    private let onSuccess: () -> Void
    private let onCancel: () -> Void

    /// Creates the MFA challenge view with the given view model and success callback.
    public init(
        viewModel: MFAChallengeViewModel,
        onSuccess: @escaping () -> Void,
        onCancel: @escaping () -> Void = {}
    ) {
        self.viewModel = viewModel
        self.onSuccess = onSuccess
        self.onCancel = onCancel
    }

    public var body: some View {
        VStack(spacing: Spacing.lg) {
            Spacer()
            headerSection
            inputSection
            Spacer()
        }
        .padding(Spacing.lg)
        .navigationTitle(UiMessages.string(.nativeAuthTwoFactorTitle, locale: locale))
        #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
        #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(UiMessages.string(.commonCancel, locale: locale)) {
                        onCancel()
                        dismiss()
                    }
                }
            }
            .onChange(of: viewModel.didSucceed) { _, succeeded in
                if succeeded {
                    onSuccess()
                }
            }
    }

    private var headerSection: some View {
        VStack(spacing: Spacing.sm) {
            Image(systemName: "lock.shield.fill")
                .font(.system(size: 56))
                .foregroundStyle(Colors.primary)
            Text(UiMessages.string(.nativeAuthAuthenticatorTitle, locale: locale))
                .font(Typography.headline)
            Text(UiMessages.string(.nativeAuthAuthenticatorDescription, locale: locale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .multilineTextAlignment(.center)
        }
    }

    private var inputSection: some View {
        VStack(spacing: Spacing.md) {
            TextField(UiMessages.string(.nativeAuthCodePlaceholder, locale: locale), text: $viewModel.code)
                .textFieldStyle(.roundedBorder)
            #if os(iOS)
                .keyboardType(.numberPad)
                .textContentType(.oneTimeCode)
            #endif
                .submitLabel(.go)
                .onSubmit { Task { await viewModel.submitCode() } }

            if let error = viewModel.errorMessage {
                Text(verbatim: UiMessages.string(error, locale: locale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }

            Button {
                Task { await viewModel.submitCode() }
            } label: {
                Group {
                    if viewModel.isLoading {
                        ProgressView()
                    } else {
                        Text(UiMessages.string(.nativeAuthVerify, locale: locale))
                    }
                }
                .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(viewModel.code.isEmpty || viewModel.isLoading)
        }
        .padding(.horizontal, Spacing.xl)
    }
}
