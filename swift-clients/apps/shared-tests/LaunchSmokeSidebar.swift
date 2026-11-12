#if os(macOS)
    import Foundation
    import XCTest

    enum LaunchSmokeSidebar {
        static func click(_ label: String, in app: XCUIApplication) {
            let row = app.outlines.staticTexts.matching(displayedText(label)).firstMatch
            XCTAssertTrue(row.waitForExistence(timeout: 20), "Missing sidebar row \(label)")
            if row.isHittable {
                row.click()
            } else {
                row.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
        }

        static func visibleText(_ value: String, in app: XCUIApplication) -> XCUIElement {
            app.staticTexts.matching(displayedText(value)).firstMatch
        }

        private static func displayedText(_ value: String) -> NSPredicate {
            NSPredicate(format: "value == %@ OR label == %@", value, value)
        }
    }
#endif
