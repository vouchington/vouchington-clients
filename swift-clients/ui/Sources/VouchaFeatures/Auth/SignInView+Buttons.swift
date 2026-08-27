import AuthenticationServices
import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

extension SignInView {
    var brandingSection: some View {
        VStack(spacing: Spacing.sm) {
            Image(systemName: "person.circle.fill")
                .font(.system(size: 64))
                .foregroundStyle(Colors.primary)
            Text(UiMessages.string(.nativeAuthSignInTitle, locale: locale))
                .font(Typography.largeTitle)
                .fontWeight(.bold)
            Text(UiMessages.string(.nativeAuthJoinDescription, locale: locale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .multilineTextAlignment(.center)
        }
    }

    var authButtonsSection: some View {
        VStack(spacing: Spacing.md) {
            emailButton
            VStack(spacing: Spacing.sm) {
                appleSignInButton
                passkeyButton
                ForEach(NativeOAuthProvider.allCases) { provider in
                    if nativeOAuthAuthorizationCoordinator?.supports(
                        provider: provider,
                        purpose: .authenticate
                    ) == true {
                        oauthButton(provider)
                    }
                }
                if nativeOAuthAuthorizationCoordinator?.canCancelPendingAuthorization == true {
                    Button(UiMessages.string(.commonCancel, locale: locale), role: .cancel) {
                        cancelNativeOAuthAuthorization()
                    }
                }
            }
        }
        .padding(.horizontal, Spacing.xl)
    }

    private func oauthButton(_ provider: NativeOAuthProvider) -> some View {
        Button {
            Task { await beginNativeOAuthSignIn(provider) }
        } label: {
            Text(UiMessages.string(provider.titleKey, locale: locale))
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.bordered)
        .controlSize(.large)
        .disabled(nativeOAuthAuthorizationCoordinator?.canStartAuthorization != true)
    }

    private var emailButton: some View {
        Button {
            showingEmailOTP = true
        } label: {
            Label(UiMessages.string(.nativeAuthContinueWithEmail, locale: locale), systemImage: "envelope")
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.borderedProminent)
        .controlSize(.large)
    }

    private var appleSignInButton: some View {
        SignInWithAppleButton(.continue) { request in
            let nonce = Self.makeAppleNonce()
            appleSignInNonce = nonce.raw
            request.nonce = nonce.sha256
            request.requestedScopes = [.fullName, .email]
        } onCompletion: { result in
            handleAppleSignIn(result: result)
        }
        .frame(height: 44)
        .cornerRadius(Spacing.sm)
    }

    private var passkeyButton: some View {
        Button {
            handlePasskeySignIn()
        } label: {
            Label(
                UiMessages.string(
                    isPasskeyLoading ? .nativeAuthCheckingPasskey : .nativeAuthUsePasskey,
                    locale: locale
                ),
                systemImage: "key.fill"
            )
            .frame(maxWidth: .infinity)
        }
        .buttonStyle(.bordered)
        .controlSize(.large)
        .disabled(isPasskeyLoading)
    }
}
