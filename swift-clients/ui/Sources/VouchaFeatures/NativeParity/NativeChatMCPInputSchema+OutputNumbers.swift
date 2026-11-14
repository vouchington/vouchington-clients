import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    struct OutputNumber {
        let negative: Bool
        let digits: [UInt8]
        let exponent: Int
    }

    static func checkOutputNumber(
        _ value: DecodedJSONValue, fields: DecodedJSONObject, remainingWork: inout Int
    ) throws -> Bool {
        guard let text = outputNumberText(value) else { return true }
        guard text.count <= remainingWork else { throw Failure.unsupportedSchema }
        remainingWork -= text.count
        guard let number = canonicalOutputNumber(text) else { return false }
        for keyword in ["minimum", "maximum"] {
            guard let bound = fields[keyword] else { continue }
            guard let boundText = outputNumberText(bound), boundText.count <= remainingWork else {
                throw Failure.unsupportedSchema
            }
            remainingWork -= boundText.count
            guard let limit = canonicalOutputNumber(boundText) else { return false }
            let comparison = compareOutputNumbers(number, limit)
            if keyword == "minimum", comparison < 0 { return false }
            if keyword == "maximum", comparison > 0 { return false }
        }
        return true
    }

    static func outputNumberText(_ value: DecodedJSONValue) -> String? {
        switch value {
        case let .number(number): String(number)
        case let .integer(number): String(number)
        case let .unsignedInteger(number): String(number)
        case let .decimal(number): NSDecimalNumber(decimal: number).stringValue
        default: nil
        }
    }

    static func canonicalOutputNumber(_ text: String) -> OutputNumber? {
        let negative = text.hasPrefix("-")
        let unsigned = negative ? String(text.dropFirst()) : text
        let parts = unsigned.lowercased().split(separator: "e", maxSplits: 1)
        guard let mantissa = parts.first else { return nil }
        let exponent = parts.count == 2 ? Int(parts[1]) : 0
        guard let exponent else { return nil }
        let decimals = mantissa.split(separator: ".", maxSplits: 1, omittingEmptySubsequences: false)
        let fractionCount = decimals.count == 2 ? decimals[1].count : 0
        let digits = Array(decimals.joined().utf8)
        guard !digits.isEmpty, digits.allSatisfy({ (48 ... 57).contains($0) }) else { return nil }
        guard let first = digits.firstIndex(where: { $0 != 48 }),
              let last = digits.lastIndex(where: { $0 != 48 }) else {
            return OutputNumber(negative: false, digits: [48], exponent: 0)
        }
        return OutputNumber(
            negative: negative, digits: Array(digits[first ... last]),
            exponent: exponent - fractionCount + digits.count - last - 1
        )
    }

    static func compareOutputNumbers(_ left: OutputNumber, _ right: OutputNumber) -> Int {
        if left.digits == [48] || right.digits == [48] {
            if left.digits == right.digits { return 0 }
            if left.digits == [48] { return right.negative ? 1 : -1 }
            return left.negative ? -1 : 1
        }
        if left.negative != right.negative { return left.negative ? -1 : 1 }
        let leftOrder = left.exponent + left.digits.count
        let rightOrder = right.exponent + right.digits.count
        var comparison = leftOrder == rightOrder ? 0 : leftOrder < rightOrder ? -1 : 1
        if comparison == 0 {
            for index in 0 ..< max(left.digits.count, right.digits.count) {
                let first = index < left.digits.count ? left.digits[index] : 48
                let second = index < right.digits.count ? right.digits[index] : 48
                if first != second {
                    comparison = first < second ? -1 : 1
                    break
                }
            }
        }
        return left.negative ? -comparison : comparison
    }
}
