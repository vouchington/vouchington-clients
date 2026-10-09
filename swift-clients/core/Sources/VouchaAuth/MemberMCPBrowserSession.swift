#if canImport(AuthenticationServices) && !targetEnvironment(macCatalyst)
    import AuthenticationServices
    import Foundation
    import VouchaAPI

    @MainActor
    @available(iOS 17.4, macOS 14.4, *)
    struct MemberMCPBrowserSession {
        typealias Completion = @MainActor @Sendable (URL?, Error?) -> Void
        typealias Factory = (MemberMCPOAuthRequest, @escaping Completion) -> MemberMCPBrowserSession

        let start: () -> Bool
        let cancel: () -> Void
        let setPresentationContext: (any ASWebAuthenticationPresentationContextProviding) -> Void

        static func systemBrowser(
            request: MemberMCPOAuthRequest, completion: @escaping Completion
        ) -> MemberMCPBrowserSession {
            guard let host = request.redirectURI.host else {
                preconditionFailure("Missing OAuth callback host")
            }
            let session = ASWebAuthenticationSession(
                url: request.authorizationURL,
                callback: .https(host: host, path: request.redirectURI.path)
            ) { callbackURL, error in
                Task { @MainActor in completion(callbackURL, error) }
            }
            return MemberMCPBrowserSession(
                start: { session.start() }, cancel: { session.cancel() },
                setPresentationContext: { session.presentationContextProvider = $0 }
            )
        }
    }
#endif
