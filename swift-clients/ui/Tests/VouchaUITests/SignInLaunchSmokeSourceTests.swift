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
        XCTAssertTrue(flow.contains("continueWithEmail.waitForExistence(timeout: LaunchSmokeTiming.interaction)"))
        XCTAssertFalse(flow.contains("app.buttons[\"Sign In\"]"))
    }

    func testRootShellSmokeWaitsForHittabilityThroughSnapshotStalls() throws {
        let smoke = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTests.swift")
        let base = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTestCase.swift")

        // CI run 44980841268: a bare isHittable stalled on snapshots for 99 s and failed unlogged.
        XCTAssertFalse(smoke.contains("XCTAssertTrue(signIn.isHittable)"))
        XCTAssertTrue(smoke.contains("waitForHittable(signIn"))
        XCTAssertTrue(base.contains("pollThroughSnapshotStalls(\"Hittability wait\")"))
        XCTAssertTrue(base.contains("element.exists && element.isHittable"))
        XCTAssertTrue(base.contains("LaunchSmokeTiming.hittability"))
        let timing = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTiming.swift")
        // Must exceed one observed ~100 s three-attempt stall so a recovered runner gets a re-check.
        XCTAssertTrue(timing.contains("static let hittability: TimeInterval = 150"))
    }

    func testNoLaunchSmokePollsBareIsHittable() throws {
        let directory = try repoURL("swift-clients/apps/shared-tests")
        let files = try FileManager.default.contentsOfDirectory(atPath: directory.path)
            .filter { $0.hasSuffix(".swift") && $0 != "LaunchSmokeTestCase.swift" }
        XCTAssertFalse(files.isEmpty)
        // CI run 45042621815: one isHittable snapshot blocked 29 s inside a 30 s expectation wait.
        // Only LaunchSmokeTestCase may read isHittable; flows use waitForHittable,
        // pollThroughSnapshotStalls and isHittableAndEnabled, which run under the tolerant helper.
        for file in files {
            let source = try String(contentsOf: directory.appendingPathComponent(file), encoding: .utf8)
            let code = source.split(separator: "\n").filter {
                !$0.trimmingCharacters(in: .whitespaces).hasPrefix("//")
            }
            XCTAssertFalse(
                code.contains { $0.range(of: #"\bisHittable\b"#, options: .regularExpression) != nil },
                "\(file) reads a bare isHittable outside LaunchSmokeTestCase"
            )
        }

        let base = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTestCase.swift")
        XCTAssertTrue(base.contains("func pollThroughSnapshotStalls("))
        XCTAssertTrue(base.contains("let stalls = toleratingSnapshotTimeouts(label)"))
        let flow = try repoSource("swift-clients/apps/shared-tests/SignInFlowTests.swift")
        XCTAssertTrue(flow.contains("pollThroughSnapshotStalls(\"Hittable \\(label) button wait\")"))
        XCTAssertTrue(flow.contains("pollThroughSnapshotStalls(\"Sidebar toggle wait\")"))
        XCTAssertFalse(flow.contains("expectation(for: ready"))
        XCTAssertFalse(flow.contains("expectation(for: sidebarReady"))
    }

    func testTopOfScreenTapWaitsForSystemBannerAndReportsItOnFailure() throws {
        let flow = try repoSource("swift-clients/apps/shared-tests/SignInFlowTests.swift")
        let banner = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeSystemBanner.swift")

        // Issue #713: a SpringBoard notification banner over the nav bar swallowed the tap.
        // A slow SpringBoard snapshot ends the wait without failing; the post-tap check still reports a banner.
        XCTAssertTrue(flow.contains("toleratingSnapshotTimeouts(\"SpringBoard banner wait\")"))
        XCTAssertTrue(flow.contains("LaunchSmokeSystemBanner.waitUntilClear(timeout: timeout)"))
        XCTAssertTrue(banner.contains("XCTWaiter().wait(for: [clear], timeout: timeout)"))
        XCTAssertTrue(flow.contains("LaunchSmokeSystemBanner.describe(app: app)"))
        XCTAssertTrue(banner.contains("com.apple.springboard"))
    }

    func testTapPreparationTakesOneSnapshotAndSkipsSpringBoardBelowTheBannerBand() throws {
        let flow = try repoSource("swift-clients/apps/shared-tests/SignInFlowTests.swift")
        let banner = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeSystemBanner.swift")

        // CI run 45048449007: each XCUIElement property read was a separate 8 s snapshot on a cold
        // Simulator, and every tap added a whole-tree SpringBoard snapshot that once stalled 29 s.
        let base = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTestCase.swift")
        let frameBody = try functionBody("func hittableFrame(", in: base)
        let frameRead = codeLines(of: frameBody)
        let snapshotReads = try occurrences(of: #"\bsnapshot\(\)"#, in: frameRead)
        let hittableReads = try occurrences(of: #"\bisHittable\b"#, in: frameRead)
        // The one place that resolves a candidate: exactly one snapshot and one hittability read.
        XCTAssertEqual(snapshotReads, 1)
        XCTAssertEqual(hittableReads, 1)

        // The tap flow must not read candidate properties itself; each read is another snapshot.
        let tapBody = try functionBody("func tapHittableButton(", in: flow)
        let tap = codeLines(of: tapBody)
        let helperCalls = try occurrences(of: #"hittableFrame\(labeled:"#, in: tap)
        let directReads = try occurrences(
            of: #"(?:\b(?:candidate|button)|\$0)\.(label|frame|isEnabled|isHittable|snapshot)\b"#,
            in: tap
        )
        XCTAssertEqual(helperCalls, 1)
        XCTAssertEqual(directReads, 0)
        XCTAssertTrue(flow.contains("LaunchSmokeSystemBanner.canCover(buttonFrame)"))
        XCTAssertTrue(banner.contains("frame.minY < topBand"))
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

    /// Text between the braces of the function whose declaration contains `signature`.
    private func functionBody(_ signature: String, in source: String) throws -> String {
        let declaration = try XCTUnwrap(source.range(of: signature))
        let open = try XCTUnwrap(source.range(of: "{\n", range: declaration.upperBound ..< source.endIndex))
        var depth = 1
        var index = open.upperBound
        while index < source.endIndex, depth > 0 {
            if source[index] == "{" { depth += 1 }
            if source[index] == "}" { depth -= 1 }
            index = source.index(after: index)
        }
        return String(source[open.upperBound ..< source.index(before: index)])
    }

    private func codeLines(of source: String) -> String {
        source.split(separator: "\n")
            .filter { !$0.trimmingCharacters(in: .whitespaces).hasPrefix("//") }
            .joined(separator: "\n")
    }

    private func occurrences(of pattern: String, in source: String) throws -> Int {
        try NSRegularExpression(pattern: pattern)
            .numberOfMatches(in: source, range: NSRange(source.startIndex..., in: source))
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        try String(contentsOf: repoURL(relative, filePath: filePath), encoding: .utf8)
    }

    private func repoURL(_ relative: String, filePath: StaticString = #filePath) throws -> URL {
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        var roots = [current, URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()]
        for _ in 0 ..< 16 {
            roots.append(roots[roots.count - 1].deletingLastPathComponent())
        }
        for root in roots {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
        }
        throw XCTSkip("Could not find \(relative)")
    }
}
