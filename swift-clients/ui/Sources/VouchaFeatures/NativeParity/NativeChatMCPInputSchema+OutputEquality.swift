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
            guard first.count == second.count else { return false }
            for (key, value) in first {
                guard let expected = second[key],
                      try sameOutput(value, expected, remainingWork: &remainingWork) else { return false }
            }
            return true
        default: return same(left, right)
        }
    }
}
