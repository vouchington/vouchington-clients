import SwiftUI
import UIKit
import VouchaAuth
@testable import VouchaFeatures
import XCTest

@MainActor
extension MutationChallengePresentationTests {
    func testSharedBrokerPresentsOnceInActiveWindowAndInactiveTeardownDoesNotCancelIt() async throws {
        let broker = MutationChallengeBroker()
        let inactiveSheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        let (inactiveWindow, inactiveRoot) = try await present(inactiveSheet, broker: broker)
        let activeSheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        let (activeWindow, _) = try await present(activeSheet, broker: broker)
        defer { broker.cancel()
            inactiveWindow.isHidden = true
            activeWindow.isHidden = true
        }
        let completed = expectation(description: "Owned mutation resumes")
        var proof: String?
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel() }
        let challenge = try await presentedChallenge(above: activeSheet)
        let id = try XCTUnwrap(broker.pendingID)
        XCTAssertTrue(challenge.view.window === activeWindow)
        inactiveWindow.makeKeyAndVisible()
        XCTAssertNil(inactiveSheet.presentedViewController)
        XCTAssertTrue(containsAnchor(inactiveRoot))

        inactiveRoot.rootView.broker = nil
        try await waitUntil { !self.containsAnchor(inactiveRoot) }
        XCTAssertEqual(broker.pendingID, id)
        try XCTUnwrap(provider.onToken)("active-window-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "active-window-proof")
        try await waitUntil { activeSheet.presentedViewController == nil }
    }

    func testUnclaimedRequestPresentsWhenWindowBecomesKey() async throws {
        let broker = MutationChallengeBroker()
        let sheet = UIHostingController(rootView: ListsCreateSheet { _ in })
        let (window, _) = try await present(sheet, broker: broker)
        let sibling = try UIWindow(windowScene: XCTUnwrap(window.windowScene))
        sibling.rootViewController = UIViewController()
        defer { broker.cancel()
            sibling.isHidden = true
            window.isHidden = true
        }
        sibling.makeKeyAndVisible()
        try await waitUntil { sibling.isKeyWindow && !window.isKeyWindow }
        XCTAssertTrue(sibling.isKeyWindow)
        XCTAssertFalse(window.isKeyWindow)
        XCTAssertNil(provider.onToken)
        let completed = expectation(description: "Owned mutation resumes")
        var proof: String?
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel() }
        try await waitUntil { broker.pendingID != nil }
        XCTAssertTrue(sibling.isKeyWindow)
        XCTAssertFalse(window.isKeyWindow)
        XCTAssertNil(sheet.presentedViewController)
        XCTAssertNil(provider.onToken)
        window.makeKeyAndVisible()
        try await waitUntil { window.isKeyWindow && !sibling.isKeyWindow }
        XCTAssertTrue(window.isKeyWindow)
        XCTAssertFalse(sibling.isKeyWindow)
        let challenge = try await presentedChallenge(above: sheet)
        XCTAssertTrue(challenge.presentingViewController === sheet)
        XCTAssertNil(sibling.rootViewController?.presentedViewController)
        try XCTUnwrap(provider.onToken)("activated-window-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "activated-window-proof")
        try await waitUntil { sheet.presentedViewController == nil }
    }

    func containsAnchor(_ controller: UIViewController) -> Bool {
        controller is MutationChallengePresentationController || controller.children.contains { containsAnchor($0) }
    }

    func present(
        _ sheet: UIViewController,
        broker: MutationChallengeBroker
    ) async throws -> (UIWindow, MutationChallengeRootController) {
        let root = try MutationChallengeRootController(rootView: MutationChallengeTestRoot(
            broker: broker,
            baseURL: XCTUnwrap(URL(string: "https://voucha.ai")),
            provider: provider
        ))
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let window = UIWindow(windowScene: scene)
        window.rootViewController = root
        var presented = false
        defer {
            if !presented {
                window.isHidden = true
                window.rootViewController = nil
            }
        }
        window.makeKeyAndVisible()
        // Attachment/key status alone does not establish completed root appearance.
        try await waitUntil {
            root.hasAppeared && root.view.window === window && window.isKeyWindow
                && scene.activationState == .foregroundActive && root.transitionCoordinator == nil
        }
        XCTAssertTrue(root.hasAppeared)
        root.present(sheet, animated: false)
        try await waitUntil {
            root.presentedViewController === sheet && sheet.presentingViewController === root
                && sheet.viewIfLoaded?.window === window && !sheet.isBeingPresented && !sheet.isBeingDismissed
                && sheet.transitionCoordinator == nil && root.transitionCoordinator == nil
        }
        presented = true
        return (window, root)
    }

    func presentedChallenge(
        above sheet: UIViewController
    ) async throws -> UIHostingController<AnyView> {
        try await waitUntil {
            guard let challenge = sheet.presentedViewController as? UIHostingController<AnyView> else { return false }
            return challenge.presentingViewController === sheet && challenge.viewIfLoaded?.window != nil
                && !challenge.isBeingPresented && !challenge.isBeingDismissed && challenge.transitionCoordinator == nil
                && sheet.transitionCoordinator == nil
        }
        return try XCTUnwrap(sheet.presentedViewController as? UIHostingController<AnyView>)
    }

    func waitUntil(_ condition: @escaping () -> Bool, file: StaticString = #filePath, line: UInt = #line) async throws {
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in condition() }, object: nil)
        let result = await XCTWaiter.fulfillment(of: [ready], timeout: 5)
        _ = try XCTUnwrap(
            result == .completed && condition() ? true : nil,
            "Presentation state did not settle",
            file: file,
            line: line
        )
    }
}

struct MutationChallengeTestRoot: View {
    var broker: MutationChallengeBroker?
    let baseURL: URL
    let provider: MutationChallengeProviderStub

    var body: some View {
        if let broker {
            Color.clear.mutationChallengeSheet(
                broker: broker,
                siteKey: "1x00000000000000000000AA",
                baseURL: baseURL
            )
            .environment(\.mutationChallengeContent) { _, _, onToken in
                provider.onToken = onToken
                return AnyView(Color.clear)
            }
        } else {
            Color.clear
        }
    }
}

@MainActor
final class MutationChallengeProviderStub {
    var onToken: ((String) -> Void)?
}

@MainActor
final class MutationChallengeRootController: UIHostingController<MutationChallengeTestRoot> {
    private(set) var hasAppeared = false

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        hasAppeared = true
    }

    override func viewWillDisappear(_ animated: Bool) {
        super.viewWillDisappear(animated)
        hasAppeared = false
    }
}
