import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct EmailOTPCodeStep: View {
    @Environment(\.locale)
    private var locale
    @Bindable
    var viewModel: EmailOTPViewModel

    var body: some View {
        VStack(spacing: Spacing.lg) {
            Spacer()
            header
            form
            Spacer()
        }
        .padding(Spacing.lg)
        #if os(iOS)
            .safeAreaInset(edge: .bottom, spacing: Spacing.md) {
                // The number pad has no submit key and covers a centered button.
                verifyCodeButton
                    .padding(.horizontal, Spacing.xl)
                    .padding(.bottom, Spacing.sm)
            }
        #endif
    }

    private var header: some View {
        VStack(spacing: Spacing.sm) {
            Image(systemName: "number.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(Colors.primary)

            Text(UiMessages.string(.nativeAuthCheckEmail, locale: locale))
                .font(Typography.headline)

            Text(
                UiMessages.string(
                    .nativeAuthCodeSent,
                    parameters: ["email": viewModel.email],
                    locale: locale
                )
            )
            .font(Typography.subheadline)
            .foregroundStyle(Colors.secondaryLabel)
            .multilineTextAlignment(.center)
        }
    }

    private var form: some View {
        VStack(spacing: Spacing.md) {
            TextField(UiMessages.string(.nativeAuthCodePlaceholder, locale: locale), text: $viewModel.code)
                .textFieldStyle(.roundedBorder)
            #if os(iOS)
                .keyboardType(.numberPad)
                .textContentType(.oneTimeCode)
            #endif
                .submitLabel(.go)
                .onSubmit { Task { await viewModel.verifyCode() } }

            if let error = viewModel.errorMessage {
                Text(verbatim: UiMessages.string(error, locale: locale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }

            #if !os(iOS)
                verifyCodeButton
            #endif

            Button(UiMessages.string(.nativeAuthDifferentEmail, locale: locale)) {
                viewModel.step = .enterEmail
                viewModel.code = ""
                viewModel.errorMessage = nil
            }
            .buttonStyle(.plain)
            .foregroundStyle(Colors.secondaryLabel)
            .font(Typography.subheadline)
        }
        .padding(.horizontal, Spacing.xl)
    }

    private var verifyCodeButton: some View {
        Button {
            Task { await viewModel.verifyCode() }
        } label: {
            if viewModel.isLoading {
                ProgressView()
            } else {
                Text(UiMessages.string(.nativeAuthVerifyCode, locale: locale))
            }
        }
        .frame(maxWidth: .infinity)
        .buttonStyle(.borderedProminent)
        .controlSize(.large)
        .disabled(viewModel.code.isEmpty || viewModel.isLoading)
    }
}
