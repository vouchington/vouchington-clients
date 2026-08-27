import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct EmailOTPEmailStep: View {
    @Environment(\.locale)
    private var locale
    @Bindable
    var viewModel: EmailOTPViewModel
    @Binding
    var showingTurnstile: Bool

    var body: some View {
        VStack(spacing: Spacing.lg) {
            Spacer()
            header
            form
            Spacer()
        }
        .padding(Spacing.lg)
    }

    private var header: some View {
        VStack(spacing: Spacing.sm) {
            Image(systemName: "envelope.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(Colors.primary)

            Text(UiMessages.string(.nativeAuthEnterEmail, locale: locale))
                .font(Typography.headline)

            Text(UiMessages.string(.nativeAuthEmailDescription, locale: locale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .multilineTextAlignment(.center)
        }
    }

    private var form: some View {
        VStack(spacing: Spacing.md) {
            TextField(UiMessages.string(.nativeAuthEmailAddress, locale: locale), text: $viewModel.email)
                .textFieldStyle(.roundedBorder)
            #if os(iOS)
                .keyboardType(.emailAddress)
                .textContentType(.emailAddress)
                .autocorrectionDisabled()
                .textInputAutocapitalization(.never)
            #endif
                .submitLabel(.send)
                .onSubmit { Task { await viewModel.requestCode() } }

            if let error = viewModel.errorMessage {
                Text(verbatim: UiMessages.string(error, locale: locale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }

            Button {
                showingTurnstile = true
            } label: {
                Label(
                    UiMessages.string(
                        viewModel.turnstileToken == nil ? .nativeAuthVerify : .nativeAuthVerified,
                        locale: locale
                    ),
                    systemImage: viewModel.turnstileToken == nil ? "checkmark.shield" : "checkmark.shield.fill"
                )
                .frame(maxWidth: .infinity)
            }
            .buttonStyle(.bordered)
            .controlSize(.large)

            Button {
                Task { await viewModel.requestCode() }
            } label: {
                if viewModel.isLoading {
                    ProgressView()
                } else {
                    Text(UiMessages.string(.nativeAuthSendCode, locale: locale))
                }
            }
            .frame(maxWidth: .infinity)
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(!viewModel.canRequestCode)
        }
        .padding(.horizontal, Spacing.xl)
    }
}
