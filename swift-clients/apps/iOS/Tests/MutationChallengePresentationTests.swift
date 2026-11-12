import SwiftUI
import UIKit
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class MutationChallengePresentationTests: XCTestCase {
    let provider = MutationChallengeProviderStub()

    func testCreateListSheetReceivesProofBeforeAwaitedSaveCompletes() async throws {
        let broker = MutationChallengeBroker()
        let completed = expectation(description: "Awaited create-list save completes")
        var proof: String?
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CreateListChallengeURLProtocol.self],
            bootstrapSession: false
        )
        await client.setMutationChallengeHandler { await broker.request() }
        let sheet = UIHostingController(rootView: ListsCreateSheet { _ in
            do {
                let response = try await client.data(for: .createList(name: "New list", visibility: "private"))
                proof = String(data: response, encoding: .utf8)
            } catch {
                XCTFail("Create-list mutation failed: \(error)")
            }
            completed.fulfill()
        })
        let (window, root) = try await present(sheet, broker: broker)
        defer { broker.cancel()
            window.isHidden = true
        }
        let save = Task { await sheet.rootView.onSave("New list") }
        defer { save.cancel() }

        let challenge = try await presentedChallenge(above: sheet)
        XCTAssertTrue(root.presentedViewController === sheet)
        XCTAssertTrue(challenge.presentingViewController === sheet)
        XCTAssertNil(proof)
        try XCTUnwrap(provider.onToken)("provider-proof")

        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "provider-proof")
        try await waitUntil { sheet.presentedViewController == nil }
        XCTAssertTrue(root.presentedViewController === sheet)
        XCTAssertNil(broker.pendingID)
    }

    func testCancellationDismissesOnlyChallengeAndRejectsLateProviderProof() async throws {
        let broker = MutationChallengeBroker()
        let sheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        let (window, root) = try await present(sheet, broker: broker)
        defer { broker.cancel()
            window.isHidden = true
        }
        let completed = expectation(description: "Cancelled mutation resumes")
        var proof: String? = "unresolved"
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel() }
        _ = try await presentedChallenge(above: sheet)
        request.cancel()
        await fulfillment(of: [completed], timeout: 2)
        try await waitUntil { sheet.presentedViewController == nil }
        try XCTUnwrap(provider.onToken)("late-proof")
        XCTAssertNil(proof)
        XCTAssertNil(broker.pendingID)
        XCTAssertTrue(root.presentedViewController === sheet)
    }

    func testFullScreenCreateListCoverStillReceivesChallengeInItsOwnWindow() async throws {
        let broker = MutationChallengeBroker()
        let sheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        sheet.modalPresentationStyle = .fullScreen
        let (window, root) = try await present(sheet, broker: broker)
        let unrelatedWindow = try UIWindow(windowScene: XCTUnwrap(window.windowScene))
        unrelatedWindow.rootViewController = UIViewController()
        defer { broker.cancel()
            unrelatedWindow.isHidden = true
            window.isHidden = true
        }
        let completed = expectation(description: "Owned mutation resumes")
        var proof: String?
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel() }
        let challenge = try await presentedChallenge(above: sheet)
        unrelatedWindow.makeKeyAndVisible()
        XCTAssertTrue(challenge.view.window === window)
        XCTAssertNil(unrelatedWindow.rootViewController?.presentedViewController)
        XCTAssertTrue(root.presentedViewController === sheet)
        try XCTUnwrap(provider.onToken)("cover-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "cover-proof")
        try await waitUntil { sheet.presentedViewController == nil }
    }

    func testAdaptiveDismissalResumesWithoutProofAndLeavesEditorOpen() async throws {
        let broker = MutationChallengeBroker()
        let sheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        let (window, root) = try await present(sheet, broker: broker)
        defer { broker.cancel()
            window.isHidden = true
        }
        let completed = expectation(description: "Dismissed mutation resumes")
        var proof: String? = "unresolved"
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel() }
        let challenge = try await presentedChallenge(above: sheet)
        let presentation = try XCTUnwrap(challenge.presentationController)
        let delegate = try XCTUnwrap(presentation.delegate)
        let lateProof = try XCTUnwrap(provider.onToken)
        challenge.dismiss(animated: false)
        try await waitUntil {
            challenge.presentingViewController == nil && sheet.presentedViewController == nil
                && challenge.viewIfLoaded?.window == nil && challenge.transitionCoordinator == nil
                && sheet.transitionCoordinator == nil && !challenge.isBeingPresented
                && !challenge.isBeingDismissed && !sheet.isBeingDismissed
        }
        XCTAssertNil(challenge.presentingViewController)
        delegate.presentationControllerDidDismiss?(presentation)
        await fulfillment(of: [completed], timeout: 2)
        lateProof("late-dismissed-proof")
        XCTAssertNil(proof)
        XCTAssertNil(broker.pendingID)
        XCTAssertNil(sheet.presentedViewController)
        XCTAssertTrue(root.presentedViewController === sheet)
    }

}

private final class CreateListChallengeURLProtocol: URLProtocol {
    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped, let url = request.url else { return }
        let proof = request.value(forHTTPHeaderField: "x-voucha-bot-challenge-token")
        let body = proof ?? #"{"code":"BOT_CHALLENGE_REQUIRED","action":"bot-challenge.mutation","message":"Verify"}"#
        guard let response = HTTPURLResponse(
            url: url,
            statusCode: proof == nil ? 403 : 200,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        ) else { return }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        stopped = true
    }
}
