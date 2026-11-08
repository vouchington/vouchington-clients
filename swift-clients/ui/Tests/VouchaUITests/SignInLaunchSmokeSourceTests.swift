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

    func testSignedOutSubsectionControlDoesNotUseTheFixedSizePicker() throws {
        let navigation = try repoSource("swift-clients/apps/shared-app/SectionDetailView+Navigation.swift")
        let picker = try repoSource("swift-clients/apps/shared-app/SectionDetailView+SubsectionToolbar.swift")
        let signedOutMenu = try XCTUnwrap(picker.components(separatedBy: "var signedOutSubsectionMenu").last)

        XCTAssertFalse(navigation.contains(".fixedSize()"))
        XCTAssertTrue(picker.contains("factory.sessionManager.isSignedIn"))
        XCTAssertTrue(signedOutMenu.contains("Menu {"))
        XCTAssertFalse(signedOutMenu.contains(".fixedSize()"))

        let toolbarItem = try XCTUnwrap(
            picker.components(separatedBy: "var verticalPickerToolbarItem").last?
                .components(separatedBy: "private var signedOutToolbarSubsections").first
        )
        let branches = toolbarItem.components(separatedBy: "} else if")
        XCTAssertEqual(branches.count, 2)
        XCTAssertTrue(branches[0].contains("factory.sessionManager.isSignedIn"))
        XCTAssertFalse(branches[0].contains("!factory.sessionManager.isSignedIn"))
        XCTAssertTrue(branches[0].contains("signedInSubsectionPicker"))
        XCTAssertFalse(branches[0].contains("signedOutSubsectionMenu"))
        XCTAssertTrue(branches[1].contains("signedOutSubsectionMenu"))
        XCTAssertFalse(branches[1].contains("signedInSubsectionPicker"))
        XCTAssertFalse(toolbarItem.contains(".fixedSize()"))
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
