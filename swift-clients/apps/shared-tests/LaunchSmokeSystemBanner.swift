#if os(iOS)
    import XCTest

    /// SpringBoard notification banners, which are drawn above the app and cannot be seen
    /// through the app's own accessibility tree.
    ///
    /// A fresh CI Simulator posts system banners (for example "Ready for Apple Intelligence")
    /// a few minutes after boot. A banner covers the navigation bar, `isHittable` on the app's
    /// button cannot see it, and XCUITest's interruption check does not treat it as an alert. A
    /// tap synthesized while one is showing lands on the banner, opens Settings and backgrounds
    /// the app (issue #713). Taps on top-of-screen controls therefore wait for a clear banner
    /// area first, and a tap that still leaves the app reports what SpringBoard was showing.
    enum LaunchSmokeSystemBanner {
        private static let springboardBundleIdentifier = "com.apple.springboard"

        private static var springboard: XCUIApplication {
            XCUIApplication(bundleIdentifier: springboardBundleIdentifier)
        }

        /// Distance from the top of the screen a banner can reach: the status-bar inset (59 pt on
        /// the largest iPhone) plus a banner of up to about 130 pt at large text sizes.
        static let topBand: CGFloat = 200

        /// Whether a banner could receive a tap on `frame`.
        static func canCover(_ frame: CGRect) -> Bool {
            frame.minY < topBand
        }

        /// Notification banners (`NotificationShortLookView` and its siblings) in SpringBoard.
        private static var banners: XCUIElementQuery {
            springboard.descendants(matching: .any)
                .matching(NSPredicate(format: "identifier BEGINSWITH 'Notification'"))
        }

        /// Blocks until no banner is on screen, so the next tap cannot land on one.
        /// Banners dismiss themselves, so this is bounded by one banner lifetime.
        ///
        /// Running out of time here is not a failure: on a starved runner one SpringBoard snapshot
        /// can take about 30 seconds (CI run 44105473901), which exhausts the wait with no banner
        /// on screen. The caller's next assertion still fails, with `describe(app:)`, if the tap
        /// then landed on a banner.
        static func waitUntilClear(timeout: TimeInterval) {
            let clear = XCTNSPredicateExpectation(
                predicate: NSPredicate { _, _ in !banners.firstMatch.exists },
                object: nil
            )
            _ = XCTWaiter().wait(for: [clear], timeout: timeout)
        }

        /// App state and SpringBoard banner text, for failure messages after a tap whose result
        /// never appeared (a tap that opened Settings leaves the app in the background).
        static func describe(app: XCUIApplication) -> String {
            let labels = banners.allElementsBoundByIndex.flatMap { banner in
                banner.staticTexts.allElementsBoundByIndex.map(\.label)
            }
            let banner = labels.isEmpty
                ? "no SpringBoard banner"
                : "SpringBoard banner \(labels.joined(separator: " | "))"
            return "App state \(app.state.rawValue) (4 is foreground); \(banner)."
        }
    }
#endif
