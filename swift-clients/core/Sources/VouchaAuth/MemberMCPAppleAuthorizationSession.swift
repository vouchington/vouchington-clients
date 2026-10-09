#if canImport(AuthenticationServices) && !targetEnvironment(macCatalyst)
    import AuthenticationServices
    import Foundation
    import VouchaAPI

    /// The native OAuth flow uses the system browser and an associated HTTPS callback only.
    @MainActor
    @available(iOS 17.4, macOS 14.4, *)
    public final class MemberMCPAppleAuthorizationSession: NSObject,
        ASWebAuthenticationPresentationContextProviding {
        public enum Failure: Error { case alreadyAuthorizing, sessionUnavailable, missingCallback }

        private let browserFactory: MemberMCPBrowserSession.Factory
        private var session: MemberMCPBrowserSession?
        private var anchor: ASPresentationAnchor?
        private var continuation: CheckedContinuation<URL, Error>?
        private var authorizationId: UUID?

        override public init() {
            browserFactory = MemberMCPBrowserSession.systemBrowser
            super.init()
        }

        init(browserFactory: @escaping MemberMCPBrowserSession.Factory) {
            self.browserFactory = browserFactory
            super.init()
        }

        public func code(
            for request: MemberMCPOAuthRequest,
            presentationAnchor: ASPresentationAnchor
        ) async throws -> String {
            guard session == nil else { throw Failure.alreadyAuthorizing }
            guard request.redirectURI.scheme == "https", request.redirectURI.host != nil
            else { throw MemberMCPOAuthRequest.Failure.invalidMetadata }
            let authorizationId = UUID()
            self.authorizationId = authorizationId
            anchor = presentationAnchor
            let callback = try await withTaskCancellationHandler {
                try await withCheckedThrowingContinuation { continuation in
                    self.continuation = continuation
                    let session = browserFactory(request) { [weak self] callbackURL, error in
                        if let error {
                            self?.finish(authorizationId, .failure(error))
                        } else if let callbackURL {
                            self?.finish(authorizationId, .success(callbackURL))
                        } else {
                            self?.finish(authorizationId, .failure(Failure.missingCallback))
                        }
                    }
                    session.setPresentationContext(self)
                    self.session = session
                    if Task.isCancelled {
                        finish(authorizationId, .failure(CancellationError()))
                        session.cancel()
                    } else if !session.start() {
                        finish(authorizationId, .failure(Failure.sessionUnavailable))
                    }
                }
            } onCancel: {
                Task { @MainActor [weak self] in
                    guard let self, self.authorizationId == authorizationId else { return }
                    let current = session
                    finish(authorizationId, .failure(CancellationError()))
                    current?.cancel()
                }
            }
            return try request.code(from: callback)
        }

        private func finish(_ authorizationId: UUID, _ result: Result<URL, Error>) {
            guard self.authorizationId == authorizationId, let continuation else { return }
            self.authorizationId = nil
            self.continuation = nil
            session = nil
            anchor = nil
            continuation.resume(with: result)
        }

        public func presentationAnchor(for _: ASWebAuthenticationSession) -> ASPresentationAnchor {
            guard let anchor else { preconditionFailure("Missing OAuth presentation anchor") }
            return anchor
        }
    }
#endif
