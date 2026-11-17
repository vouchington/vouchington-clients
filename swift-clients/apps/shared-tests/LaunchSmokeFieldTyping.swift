import XCTest

/// Types into a launch-smoke text field, then reads the value back.
/// `XCUIElement.typeText` can drop or repeat a whole sequence. A mismatch clears
/// the field and types again, up to `maximumAttempts`, then fails with the observed value.
///
/// Retries share one deadline. On a starved runner every key event can wait the full 60 seconds
/// for the app to idle ("App animations complete notification not received"), so an unbounded
/// clear loop of up to `maximumClearStrokes` keys ran the iPad sign-in leg into its 9-minute step
/// cap with no assertion (merge-queue run 44117058993). The deadline makes that a test failure
/// that names the value read back.
enum LaunchSmokeFieldTyping {
    static let maximumAttempts = 3
    /// Time for every retry after the first typed attempt, checked between key events.
    static let retryBudget: TimeInterval = LaunchSmokeTiming.interaction * 2
    private static let maximumClearStrokes = 64

    static func type(
        _ text: String,
        into field: XCUIElement,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        var deadline: Date?
        var readBack: [String] = []
        let expired = { deadline.map { Date() >= $0 } ?? false }
        for attempt in 1 ... maximumAttempts {
            if expired() { break }
            guard focus(field) else { continue }
            if expired() { break }
            clear(field, until: deadline)
            if expired() { break }
            field.typeText(text)
            let value = rawValue(in: field)
            if value == text {
                return
            }
            readBack.append(value)
            XCTContext.runActivity(named: "Typing attempt \(attempt) read back \"\(value)\"") { _ in }
            deadline = deadline ?? Date().addingTimeInterval(retryBudget)
        }
        XCTFail(
            "Typed \(rawValue(in: field)), expected \(text); attempts read back \(readBack)",
            file: file,
            line: line
        )
    }

    private static func focus(_ field: XCUIElement) -> Bool {
        #if os(macOS)
            if field.isHittable {
                field.click()
            } else {
                field.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
            return true
        #else
            let keyboardFocused = NSPredicate(format: "hasKeyboardFocus == true")
            field.tap()
            if hasKeyboardFocus(field, matching: keyboardFocused) {
                return true
            }
            field.tap()
            return hasKeyboardFocus(field, matching: keyboardFocused)
        #endif
    }

    private static func hasKeyboardFocus(_ field: XCUIElement, matching predicate: NSPredicate) -> Bool {
        XCTWaiter().wait(
            for: [XCTNSPredicateExpectation(predicate: predicate, object: field)],
            timeout: 2
        ) == .completed
    }

    /// Select all, then move to the end. A center tap can leave the caret in the
    /// middle of a doubled value, so Delete would keep the trailing copy.
    private static func clear(_ field: XCUIElement, until deadline: Date?) {
        let expired = { deadline.map { Date() >= $0 } ?? false }
        guard !enteredText(in: field).isEmpty, !expired() else { return }
        field.typeKey("a", modifierFlags: .command)
        guard !expired() else { return }
        field.typeKey(XCUIKeyboardKey.delete.rawValue, modifierFlags: [])
        guard !expired() else { return }
        field.typeKey(XCUIKeyboardKey.end.rawValue, modifierFlags: [])
        var current = enteredText(in: field)
        var stalled = 0
        var strokes = 0
        while strokes < maximumClearStrokes, !current.isEmpty, !expired() {
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
