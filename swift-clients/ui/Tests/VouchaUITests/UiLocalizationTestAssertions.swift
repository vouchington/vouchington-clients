import Foundation
import VouchaLocalization
import XCTest

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiVerbatimText?,
    _ expression2: @autoclosure () throws -> String?,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? expression1().map { UiMessages.string($0, locale: .english) }
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiVerbatimText,
    _ expression2: @autoclosure () throws -> String,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? UiMessages.string(expression1(), locale: .english)
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertNotEqual(
    _ expression1: @autoclosure () throws -> UiVerbatimText,
    _ expression2: @autoclosure () throws -> String,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? UiMessages.string(expression1(), locale: .english)
    XCTAssertNotEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiMessage?,
    _ expression2: @autoclosure () throws -> String?,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? expression1().map { UiMessages.string($0, locale: .english) }
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiMessage,
    _ expression2: @autoclosure () throws -> String,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? UiMessages.string(expression1(), locale: .english)
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiMessageKey?,
    _ expression2: @autoclosure () throws -> String?,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? expression1().map { UiMessages.string($0, locale: .english) }
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}

func XCTAssertEqual(
    _ expression1: @autoclosure () throws -> UiMessageKey,
    _ expression2: @autoclosure () throws -> String,
    _ message: @autoclosure () -> String = "",
    file: StaticString = #filePath,
    line: UInt = #line
) {
    let localized = try? UiMessages.string(expression1(), locale: .english)
    XCTAssertEqual(localized, try? expression2(), message(), file: file, line: line)
}
