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
/// runs out. Only that one issue is set aside, only inside `toleratingSnapshotTimeouts`, and the
/// count is reported if the element never appears, so a launch that never mounts still fails at the
/// budget.
class LaunchSmokeTestCase: XCTestCase {
    static let snapshotTimeoutMessage = "Timed out while evaluating UI query"

    private var isToleratingSnapshotTimeouts = false
    private var deferredSnapshotTimeouts = 0

    override func record(_ issue: XCTIssue) {
        if isToleratingSnapshotTimeouts, Self.isSnapshotTimeout(issue) {
            deferredSnapshotTimeouts += 1
            return
        }
        super.record(issue)
    }

    /// Runs `body` with snapshot timeouts set aside, for waits whose own result already decides
    /// pass or fail. Returns how many snapshot timeouts `body` hit; a passing run logs them.
    @discardableResult
    func toleratingSnapshotTimeouts(_ label: String, _ body: () -> Void) -> Int {
        deferredSnapshotTimeouts = 0
        isToleratingSnapshotTimeouts = true
        body()
        isToleratingSnapshotTimeouts = false
        if deferredSnapshotTimeouts > 0 {
            // A passing run must still show that a snapshot stalled, or the CI log cannot tell
            // whether this tolerance is what kept the test green.
            XCTContext.runActivity(
                named: "\(label) tolerated \(deferredSnapshotTimeouts) UI snapshot timeouts"
            ) { _ in }
        }
        return deferredSnapshotTimeouts
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
        var appeared = false
        let stalls = toleratingSnapshotTimeouts("Launch readiness") {
            while !appeared, deadline.timeIntervalSinceNow > 0 {
                appeared = element.waitForExistence(timeout: deadline.timeIntervalSinceNow)
            }
        }
        XCTAssertTrue(
            appeared,
            "\(message()) within \(Int(LaunchSmokeTiming.launchReadiness)) s "
                + "(\(stalls) UI snapshot timeouts while waiting)",
            file: file,
            line: line
        )
    }

    /// Waits until `element` is hittable or `LaunchSmokeTiming.hittability` has elapsed.
    ///
    /// A bare `isHittable` resolves the element with a fresh snapshot, and on a starved runner that
    /// resolution can stall for about 30 seconds per attempt. CI run 44980841268 (PR #747) spent
    /// about 96 s in one `isHittable` call, three attempts, on `signed-out-sign-in`, which existed,
    /// and then failed `XCTAssertTrue(signIn.isHittable)` with nothing logged about the stall. The
    /// budget is `LaunchSmokeTiming.hittability`, sized for one such stall plus a re-check. This polls
    /// through snapshot timeouts the way `waitForLaunchReadiness` does, so an element that is
    /// really not hittable still fails when the budget runs out.
    func waitForHittable(
        _ element: XCUIElement,
        _ message: @autoclosure () -> String = "Element was not hittable",
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        let deadline = Date().addingTimeInterval(LaunchSmokeTiming.hittability)
        var hittable = false
        let stalls = toleratingSnapshotTimeouts("Hittability wait") {
            while !hittable, deadline.timeIntervalSinceNow > 0 {
                hittable = element.exists && element.isHittable
                if !hittable { Thread.sleep(forTimeInterval: 0.5) }
            }
        }
        XCTAssertTrue(
            hittable,
            "\(message()) within \(Int(LaunchSmokeTiming.hittability)) s "
                + "(\(stalls) UI snapshot timeouts while waiting)",
            file: file,
            line: line
        )
    }
}
