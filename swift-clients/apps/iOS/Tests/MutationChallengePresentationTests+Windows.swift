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
        await waitUntil { !self.containsAnchor(inactiveRoot) }
        XCTAssertEqual(broker.pendingID, id)
        try XCTUnwrap(provider.onToken)("active-window-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "active-window-proof")
        await waitUntil { activeSheet.presentedViewController == nil }
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
        await waitUntil { sibling.isKeyWindow && !window.isKeyWindow }
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
        await waitUntil { broker.pendingID != nil }
        XCTAssertTrue(sibling.isKeyWindow)
        XCTAssertFalse(window.isKeyWindow)
        XCTAssertNil(sheet.presentedViewController)
        XCTAssertNil(provider.onToken)
        window.makeKeyAndVisible()
        await waitUntil { window.isKeyWindow && !sibling.isKeyWindow }
        XCTAssertTrue(window.isKeyWindow)
        XCTAssertFalse(sibling.isKeyWindow)
        let challenge = try await presentedChallenge(above: sheet)
        XCTAssertTrue(challenge.presentingViewController === sheet)
        XCTAssertNil(sibling.rootViewController?.presentedViewController)
        try XCTUnwrap(provider.onToken)("activated-window-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "activated-window-proof")
        await waitUntil { sheet.presentedViewController == nil }
    }

    func containsAnchor(_ controller: UIViewController) -> Bool {
        controller is MutationChallengePresentationController || controller.children.contains { containsAnchor($0) }
    }

    func present(
        _ sheet: UIViewController,
        broker: MutationChallengeBroker
    ) async throws -> (UIWindow, UIHostingController<MutationChallengeTestRoot>) {
        let root = try UIHostingController(rootView: MutationChallengeTestRoot(
            broker: broker,
            baseURL: XCTUnwrap(URL(string: "https://voucha.ai")),
            provider: provider
        ))
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let window = UIWindow(windowScene: scene)
        window.rootViewController = root
        window.makeKeyAndVisible()
        await waitUntil { root.view.window != nil && window.isKeyWindow && scene.activationState == .foregroundActive }
        let presented = expectation(description: "Create-list editor presented")
        root.present(sheet, animated: false) { presented.fulfill() }
        await fulfillment(of: [presented], timeout: 5)
        return (window, root)
    }

    func presentedChallenge(
        above sheet: UIViewController
    ) async throws -> UIHostingController<AnyView> {
        await waitUntil {
            sheet.presentedViewController is UIHostingController<AnyView>
                && sheet.presentedViewController?.isBeingPresented == false
        }
        return try XCTUnwrap(sheet.presentedViewController as? UIHostingController<AnyView>)
    }

    func waitUntil(_ condition: @escaping () -> Bool) async {
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in condition() }, object: nil)
        await fulfillment(of: [ready], timeout: 5)
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
