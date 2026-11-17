import XCTest

/// Base class for launch smokes that wait for the native root after `app.launch()`.
///
/// `waitForExistence` evaluates `existsNoRetry` on each poll. When one accessibility snapshot of a
/// starved app does not return, XCTest records "Failed to get matching snapshots: Timed out while
/// evaluating UI query" and the wait ends then, well inside its own deadline. CI runs 44105473901
/// (iPhone root shell, 71.8 s) and 43794940647/43794082897 (iPad News, 44.4 s, including a rerun)
/// failed this way under a 90 second `launchReadiness` budget; a longer budget would not have
/// helped, because nothing polled again after the stalled snapshot.
///
/// `waitForLaunchReadiness` keeps polling through such snapshot timeouts until the launch budget
/// runs out. Only that one issue is set aside, only during the readiness wait, and the count is
/// reported if the element never appears, so a launch that never mounts still fails at the budget.
class LaunchSmokeTestCase: XCTestCase {
    static let snapshotTimeoutMessage = "Timed out while evaluating UI query"

    private var isWaitingForLaunchReadiness = false
    private var deferredSnapshotTimeouts = 0

    override func record(_ issue: XCTIssue) {
        if isWaitingForLaunchReadiness, Self.isSnapshotTimeout(issue) {
            deferredSnapshotTimeouts += 1
            return
        }
        super.record(issue)
    }

    static func isSnapshotTimeout(_ issue: XCTIssue) -> Bool {
        issue.compactDescription.contains(snapshotTimeoutMessage)
            || (issue.detailedDescription?.contains(snapshotTimeoutMessage) ?? false)
    }

    /// Waits until `element` exists or `LaunchSmokeTiming.launchReadiness` has elapsed.
    func waitForLaunchReadiness(
        of element: XCUIElement,
        _ message: @autoclosure () -> String = "Launch did not mount the native root",
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        let deadline = Date().addingTimeInterval(LaunchSmokeTiming.launchReadiness)
        deferredSnapshotTimeouts = 0
        isWaitingForLaunchReadiness = true
        var appeared = false
        while !appeared, deadline.timeIntervalSinceNow > 0 {
            appeared = element.waitForExistence(timeout: deadline.timeIntervalSinceNow)
        }
        isWaitingForLaunchReadiness = false
        if appeared, deferredSnapshotTimeouts > 0 {
            // A passing run must still show that a snapshot stalled, or the CI log cannot tell
            // whether this tolerance is what kept the launch green.
            XCTContext.runActivity(
                named: "Launch readiness tolerated \(deferredSnapshotTimeouts) UI snapshot timeouts"
            ) { _ in }
        }
        XCTAssertTrue(
            appeared,
            "\(message()) within \(Int(LaunchSmokeTiming.launchReadiness)) s "
                + "(\(deferredSnapshotTimeouts) UI snapshot timeouts while waiting)",
            file: file,
            line: line
        )
    }
}
