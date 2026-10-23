import Foundation
import XCTest

final class SignInLaunchSmokeSourceTests: XCTestCase {
    func testCompactSignInControlIsASeparateLeadingButton() throws {
        let toolbar = try repoSource("swift-clients/apps/shared-app/SectionDetailView+Navigation.swift")
        let detail = try repoSource("swift-clients/apps/shared-app/SectionDetailView.swift")
        let root = try repoSource("swift-clients/apps/shared-app/RootView.swift")
        let compact = try repoSource("swift-clients/apps/shared-app/RootView+Platform.swift")
        let split = try repoSource("swift-clients/apps/shared-app/RootSplitView.swift")
        let votes = try repoSource("swift-clients/ui/Sources/VouchaDesignSystem/VoteControls.swift")
        let flow = try repoSource("swift-clients/apps/shared-tests/SignInFlowTests.swift")

        XCTAssertTrue(root.contains("signedOutSignInAccessibilityIdentifier = \"signed-out-sign-in\""))
        XCTAssertTrue(detail.contains(".topBarLeading"))
        XCTAssertTrue(toolbar.contains("placement: signInToolbarPlacement"))
        XCTAssertTrue(toolbar.contains("RootView.signedOutSignInAccessibilityIdentifier"))
        XCTAssertTrue(compact.contains("Self.signedOutSignInAccessibilityIdentifier"))
        XCTAssertTrue(split.contains("RootView.signedOutSignInAccessibilityIdentifier"))
        XCTAssertFalse(votes.contains("signed-out-sign-in"))
        XCTAssertTrue(flow.contains("app.buttons[\"signed-out-sign-in\"]"))
        XCTAssertTrue(flow.contains("identifier: \"signed-out-sign-in\""))
        XCTAssertTrue(flow.contains("app.buttons[\"Continue with Email\"]"))
        XCTAssertTrue(flow.contains("continueWithEmail.waitForExistence(timeout: 20)"))
        XCTAssertFalse(flow.contains("app.buttons[\"Sign In\"]"))
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        var roots = [current, URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()]
        for _ in 0 ..< 16 {
            roots.append(roots[roots.count - 1].deletingLastPathComponent())
        }
        for root in roots {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
        }
        throw XCTSkip("Could not find \(relative)")
    }
}
