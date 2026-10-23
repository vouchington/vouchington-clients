import XCTest

/// Types into a launch-smoke text field, then reads the value back.
/// `XCUIElement.typeText` can drop or repeat a whole sequence. A mismatch clears
/// the field and types again, up to `maximumAttempts`, then fails with the observed value.
enum LaunchSmokeFieldTyping {
    static let maximumAttempts = 3
    private static let maximumClearStrokes = 64

    static func type(
        _ text: String,
        into field: XCUIElement,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for _ in 1 ... maximumAttempts {
            focus(field)
            clear(field)
            field.typeText(text)
            if rawValue(in: field) == text {
                return
            }
        }
        XCTFail("Typed \(rawValue(in: field)), expected \(text)", file: file, line: line)
    }

    private static func focus(_ field: XCUIElement) {
        #if os(macOS)
            if field.isHittable {
                field.click()
            } else {
                field.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
        #else
            field.tap()
            let keyboard = XCUIApplication().keyboards.firstMatch
            if keyboard.waitForExistence(timeout: 2) {
                return
            }
            field.tap()
            _ = keyboard.waitForExistence(timeout: 2)
        #endif
    }

    /// Select all, then move to the end. A center tap can leave the caret in the
    /// middle of a doubled value, so Delete would keep the trailing copy.
    private static func clear(_ field: XCUIElement) {
        guard !enteredText(in: field).isEmpty else { return }
        field.typeKey("a", modifierFlags: .command)
        field.typeKey(XCUIKeyboardKey.delete.rawValue, modifierFlags: [])
        field.typeKey(XCUIKeyboardKey.end.rawValue, modifierFlags: [])
        var current = enteredText(in: field)
        var stalled = 0
        var strokes = 0
        while strokes < maximumClearStrokes, !current.isEmpty {
            field.typeText(XCUIKeyboardKey.delete.rawValue)
            strokes += 1
            let next = enteredText(in: field)
            if next.count >= current.count {
                stalled += 1
                if stalled == maximumAttempts {
                    return
                }
            } else {
                stalled = 0
            }
            current = next
        }
    }

    /// Empty SwiftUI fields report their prompt as `value`. That prompt is not entered text.
    private static func enteredText(in field: XCUIElement) -> String {
        guard let value = field.value as? String else { return "" }
        if value.isEmpty || value == field.placeholderValue {
            return ""
        }
        return value
    }

    private static func rawValue(in field: XCUIElement) -> String {
        field.value as? String ?? ""
    }
}
