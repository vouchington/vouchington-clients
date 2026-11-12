import SwiftUI
import VouchaAuth
import VouchaCore
import VouchaLocalization
#if canImport(AuthenticationServices)
    import AuthenticationServices
#endif

struct FreshAccountConfirmationOtherMethods: View {
    @Environment(\.locale)
    private var locale
    @Environment(\.openURL)
    private var openURL
    @Bindable
    var model: FreshAccountConfirmationViewModel

    var body: some View {
        Group {
            if model.methods?.hasPasskey == true {
                Text(UiMessages.string(.extractedMyPasskeyManagerPasskeysC87ccdb4, locale: locale))
                #if canImport(AuthenticationServices)
                    ForEach(model.passkeys) { passkey in
                        Button {
                            guard let anchor = SignInView.currentPresentationAnchor() else {
                                model.errorMessage = .init(.nativeSwiftAuthPasskeyWindowUnavailable)
                                return
                            }
                            Task { await model.verifyPasskey(id: passkey.id, anchor: anchor) }
                        } label: {
                            Text(UiMessages.string(
                                passkey.name.flatMap { $0.isEmpty ? nil : $0 }.map(UiVerbatimText.userContent)
                                    ?? .message(.nativeAuthVerify), locale: locale
                            ))
                        }
                    }
                #endif
                HybridPaginationControl(
                    hasMore: model.passkeyPagination.hasMore,
                    isLoading: model.passkeyPagination.isLoading,
                    hasError: model.passkeyPagination.lastError != nil,
                    accessibilityIdentifier: "reauth-passkeys-pagination"
                ) { await model.loadMorePasskeys() }
            }
            #if canImport(AuthenticationServices)
                appleControls
            #endif
            if model.methods?.providers.contains("google") == true {
                Button(UiMessages.string(.nativeSwiftPresentationValuesGoogle, locale: locale)) {
                    Task { await model.verifyGoogle() }
                }
                .disabled(model.googleAssertion == nil)
                if model.googleAssertion == nil {
                    Text(UiMessages.string(
                        .extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96, locale: locale
                    ))
                }
            }
            ForEach(NativeOAuthProvider.allCases.filter {
                $0 != .google && model.methods?.providers.contains($0.rawValue) == true
            }) { provider in
                Button(UiMessages.string(provider.titleKey, locale: locale)) {
                    Task { await model.beginBroker(provider: provider, openURL: { openURL($0) }) }
                }
                .disabled(model.broker == nil || model.broker?.isWorking == true)
            }
        }
        .disabled(model.isWorking)
        .onChange(of: model.broker?.result) { _, result in
            Task { await model.consumeBrokerResult(result) }
        }
    }

    #if canImport(AuthenticationServices)
        @ViewBuilder
        private var appleControls: some View {
            if model.methods?.providers.contains("apple") == true {
                if let challenge = model.appleChallenge {
                    SignInWithAppleButton(.continue) { request in
                        request.nonce = FreshAccountConfirmationService.appleNonceHash(challenge.nonce)
                    } onCompletion: { result in
                        switch result {
                        case let .success(authorization):
                            guard let credential = authorization.credential as? ASAuthorizationAppleIDCredential,
                                  let data = credential.identityToken,
                                  let token = String(data: data, encoding: .utf8) else {
                                model.errorMessage = .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
                                return
                            }
                            Task { await model.verifyApple(credential: token, flowId: challenge.flowId) }
                        case .failure:
                            model.appleChallenge = nil
                            model.errorMessage = .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
                        }
                    }
                    .frame(height: 44)
                } else {
                    Button(UiMessages.string(.nativeSwiftPresentationValuesApple, locale: locale)) {
                        Task { await model.prepareApple() }
                    }
                }
            }
        }
    #endif
}
