#if os(macOS)
    import AuthenticationServices
    import Foundation
    @testable import VouchaAPI
    @testable import VouchaAuth
    import XCTest

    @MainActor
    final class MemberMCPAppleAuthorizationSessionTests: XCTestCase {
        func testCancelledBrowsersLateCallbackDoesNotFinishReplacementAuthorization() async throws {
            guard #available(macOS 14.4, *) else { throw XCTSkip("HTTPS browser callbacks require macOS 14.4") }
            let firstRequest = try request()
            let replacementRequest = try request()
            let firstStarted = expectation(description: "first browser started")
            let replacementStarted = expectation(description: "replacement browser started")
            var browsers: [String: Browser] = [:]
            let model = MemberMCPAppleAuthorizationSession { request, completion in
                let browser = Browser(completion: completion)
                browsers[request.state] = browser
                return browser.session {
                    (request.state == firstRequest.state ? firstStarted : replacementStarted).fulfill()
                }
            }
            let anchor = ASPresentationAnchor(contentRect: .zero, styleMask: [], backing: .buffered, defer: true)
            let first = Task { try await model.code(for: firstRequest, presentationAnchor: anchor) }
            await fulfillment(of: [firstStarted], timeout: 2)
            first.cancel()
            do {
                _ = try await first.value
                XCTFail("Cancelled authorization returned a code")
            } catch is CancellationError {
                // The cancelled browser may still deliver a callback later.
            }
            let oldBrowser = try XCTUnwrap(browsers[firstRequest.state])
            XCTAssertTrue(oldBrowser.cancelled)

            let replacement = Task { try await model.code(for: replacementRequest, presentationAnchor: anchor) }
            await fulfillment(of: [replacementStarted], timeout: 2)
            let currentBrowser = try XCTUnwrap(browsers[replacementRequest.state])
            try oldBrowser.completion(callback(for: firstRequest, code: "old-code"), nil)
            try currentBrowser.completion(callback(for: replacementRequest, code: "new-code"), nil)

            let code = try await replacement.value
            XCTAssertEqual(code, "new-code")
            XCTAssertFalse(currentBrowser.cancelled)
        }

        func testUnavailableBrowserReleasesAdmissionForRetry() async throws {
            guard #available(macOS 14.4, *) else { throw XCTSkip("HTTPS browser callbacks require macOS 14.4") }
            var available = false
            let pending = try request()
            let model = MemberMCPAppleAuthorizationSession { request, completion in
                MemberMCPBrowserSession(
                    start: {
                        if available { completion(try? self.callback(for: request, code: "retry-code"), nil) }
                        return available
                    }, cancel: {}, setPresentationContext: { _ in }
                )
            }
            let anchor = ASPresentationAnchor(contentRect: .zero, styleMask: [], backing: .buffered, defer: true)
            do {
                _ = try await model.code(for: pending, presentationAnchor: anchor)
                XCTFail("Unavailable browser returned a code")
            } catch MemberMCPAppleAuthorizationSession.Failure.sessionUnavailable {
                available = true
            }
            let code = try await model.code(for: pending, presentationAnchor: anchor)
            XCTAssertEqual(code, "retry-code")
        }

        private func request() throws -> MemberMCPOAuthRequest {
            try MemberMCPOAuthRequest(
                siteOrigin: XCTUnwrap(URL(string: "https://example.test")), app: .macos,
                issuerIdentifier: "https://example.test",
                authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize")),
                redirectURI: XCTUnwrap(URL(string: "https://example.test/oauth/native/macos/callback"))
            )
        }

        private func callback(for request: MemberMCPOAuthRequest, code: String) throws -> URL {
            var parts = try XCTUnwrap(URLComponents(url: request.redirectURI, resolvingAgainstBaseURL: false))
            parts.queryItems = [
                URLQueryItem(name: "code", value: code), URLQueryItem(name: "state", value: request.state),
                URLQueryItem(name: "iss", value: request.issuerIdentifier)
            ]
            return try XCTUnwrap(parts.url)
        }

        @MainActor
        @available(macOS 14.4, *)
        private final class Browser {
            let completion: MemberMCPBrowserSession.Completion
            var cancelled = false

            init(completion: @escaping MemberMCPBrowserSession.Completion) {
                self.completion = completion
            }

            func session(started: @escaping () -> Void) -> MemberMCPBrowserSession {
                MemberMCPBrowserSession(
                    start: { started()
                        return true
                    }, cancel: { self.cancelled = true },
                    setPresentationContext: { _ in }
                )
            }
        }
    }
#endif
