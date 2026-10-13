import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    static func integer(_ value: DecodedJSONValue?) -> Int? {
        switch value {
        case let .integer(number): Int(exactly: number).flatMap { $0 >= 0 ? $0 : nil }
        case let .unsignedInteger(number): Int(exactly: number)
        case let .number(number): Int(exactly: number).flatMap { $0 >= 0 ? $0 : nil }
        case let .decimal(number): Int(NSDecimalNumber(decimal: number).stringValue).flatMap { $0 >= 0 ? $0 : nil }
        default: nil
        }
    }

    static func same(_ left: DecodedJSONValue, _ right: DecodedJSONValue) -> Bool {
        if let first = decimalNumber(left), let second = decimalNumber(right) {
            return first == second
        }
        switch (left, right) {
        case (.null, .null): return true
        case let (.bool(first), .bool(second)): return first == second
        case let (.string(first), .string(second)): return first == second
        case let (.array(first), .array(second)):
            return first.count == second.count && zip(first, second).allSatisfy { same($0, $1) }
        case let (.object(first), .object(second)):
            return first.count == second.count && first.allSatisfy { key, value in
                second[key].map { same(value, $0) } == true
            }
        default: return false
        }
    }

    static func decimalNumber(_ value: DecodedJSONValue) -> Decimal? {
        switch value {
        case let .number(number): Decimal(string: String(number), locale: Locale(identifier: "en_US_POSIX"))
        case let .integer(number): Decimal(string: String(number), locale: Locale(identifier: "en_US_POSIX"))
        case let .unsignedInteger(number): Decimal(string: String(number), locale: Locale(identifier: "en_US_POSIX"))
        case let .decimal(number): number
        default: nil
        }
    }
}
