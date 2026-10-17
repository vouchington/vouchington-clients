import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    static let boundedPatternExpressions: [String: NSRegularExpression] =
        boundedPatternSources.reduce(into: [:]) { expressions, pattern in
            expressions[pattern] = try? NSRegularExpression(pattern: pattern)
        }

    /// Output equality uses the same bounded numeric representation as output inequalities.
    static func sameOutput(
        _ left: DecodedJSONValue, _ right: DecodedJSONValue, remainingWork: inout Int
    ) throws -> Bool {
        guard remainingWork > 0 else { throw Failure.unsupportedSchema }
        remainingWork -= 1
        if let first = outputNumberText(left), let second = outputNumberText(right) {
            guard first.count <= remainingWork, second.count <= remainingWork - first.count else {
                throw Failure.unsupportedSchema
            }
            remainingWork -= first.count + second.count
            guard let number = canonicalOutputNumber(first), let expected = canonicalOutputNumber(second) else {
                return false
            }
            return compareOutputNumbers(number, expected) == 0
        }
        return try sameOutputCollection(left, right, remainingWork: &remainingWork)
    }

    private static func sameOutputCollection(
        _ left: DecodedJSONValue, _ right: DecodedJSONValue, remainingWork: inout Int
    ) throws -> Bool {
        switch (left, right) {
        case let (.array(first), .array(second)):
            guard first.count == second.count else { return false }
            for (value, expected) in zip(first, second)
                where try !sameOutput(value, expected, remainingWork: &remainingWork) {
                return false
            }
            return true
        case let (.object(first), .object(second)):
            return try sameOutputObjects(first, second, remainingWork: &remainingWork)
        case let (.string(first), .string(second)):
            try chargeOutputBytes(first.utf8.count, second.utf8.count, remainingWork: &remainingWork)
            return first.utf8.elementsEqual(second.utf8)
        default: return same(left, right)
        }
    }

    private static func chargeOutputBytes(_ first: Int, _ second: Int = 0, remainingWork: inout Int) throws {
        guard first <= remainingWork, second <= remainingWork - first else { throw Failure.unsupportedSchema }
        remainingWork -= first + second
    }

    private static func sameOutputObjects(
        _ left: [String: DecodedJSONValue], _ right: [String: DecodedJSONValue], remainingWork: inout Int
    ) throws -> Bool {
        guard left.count == right.count else { return false }
        var expected: [Data: DecodedJSONValue] = [:]
        for (key, value) in right {
            let bytes = Data(key.utf8)
            try chargeOutputBytes(bytes.count, remainingWork: &remainingWork)
            expected[bytes] = value
        }
        for (key, value) in left {
            let bytes = Data(key.utf8)
            try chargeOutputBytes(bytes.count, remainingWork: &remainingWork)
            guard let other = expected[bytes],
                  try sameOutput(value, other, remainingWork: &remainingWork) else { return false }
        }
        return true
    }

}
