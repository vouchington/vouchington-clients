import AuthenticationServices
import CryptoKit
import VouchaAuth
import VouchaLocalization
#if os(iOS)
    import UIKit
#elseif os(macOS)
    import AppKit
#endif

extension SignInView {
    static func makeAppleNonce() -> (raw: String, sha256: String) {
        let raw = UUID().uuidString + UUID().uuidString
        return (raw, raw.sha256HexDigest)
    }

    /// Handles the result from `SignInWithAppleButton`.
    @MainActor
    func handleAppleSignIn(result: Result<ASAuthorization, Error>) {
        appleSignInError = nil
        switch result {
        case let .success(authorization):
            guard let cred = authorization.credential as? ASAuthorizationAppleIDCredential,
                  let tokenData = cred.identityToken,
                  let token = String(data: tokenData, encoding: .utf8) else {
                appleSignInError = .message(.nativeSwiftAuthAppleIdentityTokenUnavailable)
                return
            }
            guard let nonce = appleSignInNonce else {
                appleSignInError = .message(.nativeSwiftAuthAppleRequestVerificationFailed)
                return
            }
            appleSignInNonce = nil
            Task { await performAppleSignIn(token: token, nonce: nonce, userName: displayName(from: cred)) }
        case let .failure(error):
            appleSignInNonce = nil
            if (error as NSError).code != ASAuthorizationError.canceled.rawValue {
                appleSignInError = .verbatim(error.localizedDescription)
            }
        }
    }

    /// Builds a display name string from Apple credential's optional full name.
    func displayName(from credential: ASAuthorizationAppleIDCredential) -> String? {
        guard let name = credential.fullName else { return nil }
        let parts = [name.givenName, name.familyName].compactMap { $0 }
        let joined = parts.joined(separator: " ")
        return joined.isEmpty ? nil : joined
    }

    /// Calls `SignInService.signInWithApple` and routes to MFA or success.
    @MainActor
    func performAppleSignIn(token: String, nonce: String, userName: String?) async {
        do {
            if let challenge = try await signInService.signInWithApple(
                identityToken: token, nonce: nonce, userName: userName
            ) {
                mfaVM = MFAChallengeViewModel(
                    loginAttemptId: challenge.loginAttemptId,
                    signInService: signInService
                )
                showingMFAChallenge = true
            } else {
                completeSuccessfulSignIn()
                dismiss()
            }
        } catch {
            appleSignInError = .verbatim(error.localizedDescription)
        }
    }

    @MainActor
    func handlePasskeySignIn() {
        passkeySignInError = nil
        isPasskeyLoading = true
        Task { await performPasskeySignIn() }
    }

    @MainActor
    func performPasskeySignIn() async {
        defer { isPasskeyLoading = false }
        do {
            guard let presentationAnchor = Self.currentPresentationAnchor() else {
                passkeySignInError = .message(.nativeSwiftAuthPasskeyWindowUnavailable)
                return
            }
            if let challenge = try await signInService.signInWithPasskey(
                presentationAnchor: presentationAnchor
            ) {
                mfaVM = MFAChallengeViewModel(
                    loginAttemptId: challenge.loginAttemptId,
                    signInService: signInService
                )
                showingMFAChallenge = true
            } else {
                completeSuccessfulSignIn()
                dismiss()
            }
        } catch {
            let nsError = error as NSError
            if nsError.domain == ASAuthorizationError.errorDomain,
               nsError.code == ASAuthorizationError.canceled.rawValue {
                passkeySignInError = .message(.nativeSwiftAuthPasskeyCancelled)
            } else {
                passkeySignInError = .verbatim(error.localizedDescription)
            }
        }
    }

    static func currentPresentationAnchor() -> ASPresentationAnchor? {
        #if os(iOS)
            let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            return scenes.flatMap(\.windows).first { $0.isKeyWindow }
        #elseif os(macOS)
            return NSApplication.shared.keyWindow ?? NSApplication.shared.windows.first
        #else
            return nil
        #endif
    }
}

private extension String {
    var sha256HexDigest: String {
        SHA256.hash(data: Data(utf8)).map {
            String(format: "%02x", $0)
        }.joined()
    }
}
