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

        private var session: ASWebAuthenticationSession?
        private var anchor: ASPresentationAnchor?
        private var continuation: CheckedContinuation<URL, Error>?

        override public init() {}

        public func code(
            for request: MemberMCPOAuthRequest,
            presentationAnchor: ASPresentationAnchor
        ) async throws -> String {
            guard session == nil else { throw Failure.alreadyAuthorizing }
            guard request.redirectURI.scheme == "https", let host = request.redirectURI.host
            else { throw MemberMCPOAuthRequest.Failure.invalidMetadata }
            anchor = presentationAnchor
            let callback = try await withTaskCancellationHandler {
                try await withCheckedThrowingContinuation { continuation in
                    self.continuation = continuation
                    let session = ASWebAuthenticationSession(
                        url: request.authorizationURL,
                        callback: .https(host: host, path: request.redirectURI.path)
                    ) { [weak self] callbackURL, error in
                        Task { @MainActor in
                            if let error {
                                self?.finish(.failure(error))
                            } else if let callbackURL {
                                self?.finish(.success(callbackURL))
                            } else {
                                self?.finish(.failure(Failure.missingCallback))
                            }
                        }
                    }
                    session.presentationContextProvider = self
                    self.session = session
                    if Task.isCancelled {
                        finish(.failure(CancellationError()))
                        session.cancel()
                    } else if !session.start() {
                        finish(.failure(Failure.sessionUnavailable))
                    }
                }
            } onCancel: {
                Task { @MainActor [weak self] in
                    guard let self else { return }
                    let current = session
                    finish(.failure(CancellationError()))
                    current?.cancel()
                }
            }
            return try request.code(from: callback)
        }

        private func finish(_ result: Result<URL, Error>) {
            guard let continuation else { return }
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
