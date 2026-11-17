import Foundation
import XCTest

/// Deadlines shared by the launch-smoke flows.
///
/// Hosted macOS runners run Xcode, the Simulator and the app under test on a few shared cores.
/// Failed CI runs showed the app launching but not yet rendering, not a missing element:
/// `Wait for ai.voucha.ios to idle` ran 23 to 27 seconds and once the full 60 second limit
/// ("App event loop idle notification not received"), automation-session setup took up to 22
/// seconds, and one tap took 8 seconds to synthesize. XCUITest evaluates each existence poll
/// through the starved app, so a nominal 20 second wait is also stretched (a CPU-saturated local
/// run spent 31 seconds inside a 20 second `waitForExistence`). One snapshot can also time out
/// outright, which ends a plain `waitForExistence` early; launch readiness therefore goes through
/// `LaunchSmokeTestCase.waitForLaunchReadiness`.
///
/// These budgets are therefore not margin for slow assertions. They bound how long a starved
/// host may take to launch and to render the result of one user action. A fixture that is
/// actually broken still fails, with the visible hierarchy, only after the budget.
enum LaunchSmokeTiming {
    /// First launch of the app, through identity restoration, until the native root is mounted.
    static let launchReadiness: TimeInterval = 90
    /// One user action (tap, typed field, sheet presentation) until its visible result.
    static let interaction: TimeInterval = 30

    /// Visible static text, for failure messages that otherwise only say "XCTAssertTrue failed".
    static func visibleText(in app: XCUIApplication, limit: Int = 24) -> String {
        let labels = app.staticTexts.allElementsBoundByIndex.map(\.label)
        return "\(labels.count) texts. Visible text: \(labels.prefix(limit).joined(separator: " | "))"
    }
}
