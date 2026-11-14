import Foundation
import SwiftUI
import ViewInspector
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import WebKit
import XCTest
#if os(macOS)
    import AppKit
#endif

@MainActor
final class NativeTurnstileCoordinatorTests: XCTestCase {
    func testCoordinatorRejectsWrongHandlerFrameTypeEmptyAndInactiveMessages() {
        var tokens: [String] = []
        let coordinator = NativeTurnstileCoordinator { tokens.append($0) }
        coordinator.receiveTokenMessage(name: "other", body: "wrong-handler", isMainFrame: true)
        coordinator.receiveTokenMessage(name: "turnstileToken", body: "subframe", isMainFrame: false)
        coordinator.receiveTokenMessage(name: "turnstileToken", body: 42, isMainFrame: true)
        coordinator.receiveTokenMessage(name: "turnstileToken", body: "", isMainFrame: true)
        XCTAssertTrue(tokens.isEmpty)

        coordinator.receiveTokenMessage(name: "turnstileToken", body: "valid-token", isMainFrame: true)
        XCTAssertEqual(tokens, ["valid-token"])
        coordinator.isActive = false
        coordinator.receiveTokenMessage(name: "turnstileToken", body: "late-token", isMainFrame: true)
        XCTAssertEqual(tokens, ["valid-token"])
    }

    func testValidChallengeBuildsWebViewWithConfiguredSiteKeyAndAction() throws {
        let challenge = try NativeTurnstileChallengeView(
            siteKey: "configured-key", action: .postsCreate,
            baseURL: XCTUnwrap(URL(string: "https://staging.example"))
        ) { _ in }
        let webView = try challenge.inspect().find(NativeTurnstileWebView.self).actualView()
        XCTAssertEqual(webView.siteKey, "configured-key")
        XCTAssertEqual(webView.challenge.action, "posts-create")
    }

    #if os(macOS)
        func testHostedChallengeCreatesAndTearsDownNativeWebView() async throws {
            let appeared = expectation(description: "Native challenge appeared")
            let challenge = try NativeTurnstileChallengeView(
                siteKey: "hosted-key", action: .postsCreate,
                baseURL: XCTUnwrap(URL(string: "https://staging.example"))
            ) { _ in }
            let controller = NSHostingController(rootView: AnyView(challenge.onAppear { appeared.fulfill() }))
            let window = NSWindow(
                contentRect: NSRect(x: 0, y: 0, width: 400, height: 320),
                styleMask: [.borderless], backing: .buffered, defer: false
            )
            window.isReleasedWhenClosed = false
            window.contentViewController = controller
            window.orderFront(nil)
            defer {
                controller.rootView = AnyView(EmptyView())
                window.close()
            }
            await fulfillment(of: [appeared], timeout: 5)
            controller.view.layoutSubtreeIfNeeded()
            let webView = try XCTUnwrap(findWebView(in: controller.view))
            webView.stopLoading()
            let loaded = expectation(description: "Hosted local page loaded")
            let navigation = LocalTurnstileNavigationDelegate(loaded: loaded)
            defer { withExtendedLifetime(navigation) {} }
            webView.navigationDelegate = navigation
            webView.loadHTMLString("<html><body>Hosted local test</body></html>", baseURL: nil)
            await fulfillment(of: [loaded], timeout: 5)
            let handlerExists = try await evaluateBoolean(
                "typeof window.webkit?.messageHandlers?.turnstileToken !== 'undefined'", in: webView
            )
            XCTAssertTrue(handlerExists)
            controller.rootView = AnyView(EmptyView())
            controller.view.layoutSubtreeIfNeeded()
        }

        private func evaluateBoolean(_ script: String, in webView: WKWebView) async throws -> Bool {
            try await withCheckedThrowingContinuation { continuation in
                webView.evaluateJavaScript(script) { value, error in
                    if let error {
                        continuation.resume(throwing: error)
                    } else {
                        continuation.resume(returning: value as? Bool ?? false)
                    }
                }
            }
        }

        private func findWebView(in view: NSView) -> WKWebView? {
            if let webView = view as? WKWebView { return webView }
            return view.subviews.lazy.compactMap { self.findWebView(in: $0) }.first
        }

        func testWebKitDeliversTokenAndDismantleRemovesHandlerAndDeactivatesCoordinator() async throws {
            let received = expectation(description: "Main-frame WebKit token delivered")
            var tokens: [String] = []
            let challenge = try XCTUnwrap(try TurnstileChallengeContext(
                actionTag: AppAttestActionTag.postsCreate.rawValue,
                baseURL: XCTUnwrap(URL(string: "https://staging.example"))
            ))
            let representable = NativeTurnstileWebView(siteKey: "test-key", challenge: challenge) { token in
                tokens.append(token)
                received.fulfill()
            }
            let coordinator = representable.makeCoordinator()
            let webView = makeTurnstileWebView(
                siteKey: representable.siteKey,
                challenge: challenge,
                coordinator: coordinator
            )
            webView.stopLoading()
            let loaded = expectation(description: "Local HTML loaded")
            let navigation = LocalTurnstileNavigationDelegate(loaded: loaded)
            defer { withExtendedLifetime(navigation) {} }
            webView.navigationDelegate = navigation
            webView.loadHTMLString("<html><body>Local test</body></html>", baseURL: challenge.origin)
            await fulfillment(of: [loaded], timeout: 5)

            let messagePosted = try await evaluateBoolean(
                "window.webkit.messageHandlers.turnstileToken.postMessage('web-kit-token'); true", in: webView
            )
            XCTAssertTrue(messagePosted)
            await fulfillment(of: [received], timeout: 5)
            XCTAssertEqual(tokens, ["web-kit-token"])

            NativeTurnstileWebView.dismantleNSView(webView, coordinator: coordinator)
            XCTAssertFalse(coordinator.isActive)
            coordinator.receiveTokenMessage(name: "turnstileToken", body: "late", isMainFrame: true)
            XCTAssertEqual(tokens, ["web-kit-token"])
            let handlerExists = try await evaluateBoolean(
                "typeof window.webkit?.messageHandlers?.turnstileToken !== 'undefined'", in: webView
            )
            XCTAssertFalse(handlerExists)
        }
    #endif
}

@MainActor
private final class LocalTurnstileNavigationDelegate: NSObject, WKNavigationDelegate {
    let loaded: XCTestExpectation

    init(loaded: XCTestExpectation) {
        self.loaded = loaded
    }

    func webView(_: WKWebView, didFinish _: WKNavigation!) {
        loaded.fulfill()
    }
}
