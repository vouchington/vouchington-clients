import Foundation
import VouchaModels

/// Checks the numeric representation that the actual model decoder and encoder retain.
/// The caller has already admitted JSON syntax, nesting, and the bounded frame size.
enum MCPJSONLosslessNumberGuard {
    static func validate(_ data: Data) throws {
        let bytes = Array(data)
        let decoder = JSONDecoder()
        let encoder = JSONEncoder()
        var index = 0
        while index < bytes.count {
            if bytes[index] == 34 {
                index += 1
                while index < bytes.count, bytes[index] != 34 {
                    index += bytes[index] == 92 ? 2 : 1
                }
                index += 1
            } else if bytes[index] == 45 || (48 ... 57).contains(bytes[index]) {
                let start = index
                while index < bytes.count, isNumberByte(bytes[index]) {
                    index += 1
                }
                let token = Data(bytes[start ..< index])
                do {
                    let original = try canonical(Array(token))
                    let value = try decoder.decode(DecodedJSONValue.self, from: token)
                    let retained = try canonical(Array(encoder.encode(value)))
                    guard original.negative == retained.negative, original.digits == retained.digits,
                          original.exponent == retained.exponent else {
                        throw MemberMCPClient.Failure.invalidResponse
                    }
                } catch {
                    throw MemberMCPClient.Failure.invalidResponse
                }
            } else {
                index += 1
            }
        }
    }

    private struct Number {
        let negative: Bool
        let digits: [UInt8]
        let exponent: Int
    }

    private static func canonical(_ bytes: [UInt8]) throws -> Number {
        let negative = bytes.first == 45
        let start = negative ? 1 : 0
        let exponentStart = bytes.firstIndex(where: { $0 == 101 || $0 == 69 }) ?? bytes.count
        let mantissa = bytes[start ..< exponentStart]
        let fractionCount = mantissa.firstIndex(of: 46).map { exponentStart - $0 - 1 } ?? 0
        let digits = mantissa.filter { $0 != 46 }
        guard let first = digits.firstIndex(where: { $0 != 48 }) else {
            return Number(negative: false, digits: [48], exponent: 0)
        }
        let last = digits.lastIndex(where: { $0 != 48 }) ?? first
        var exponent = 0
        if exponentStart < bytes.count {
            var index = exponentStart + 1
            let exponentNegative = bytes[index] == 45
            if bytes[index] == 45 || bytes[index] == 43 { index += 1 }
            // A larger nonzero exponent cannot cancel the bounded mantissa into
            // any finite representation supported by DecodedJSONValue.
            let limit = bytes.count + 1_000
            while index < bytes.count {
                let digit = Int(bytes[index] - 48)
                guard exponent <= (limit - digit) / 10 else {
                    throw MemberMCPClient.Failure.invalidResponse
                }
                exponent = exponent * 10 + digit
                index += 1
            }
            if exponentNegative { exponent = -exponent }
        }
        return Number(
            negative: negative, digits: Array(digits[first ... last]),
            exponent: exponent - fractionCount + digits.count - last - 1
        )
    }

    private static func isNumberByte(_ byte: UInt8) -> Bool {
        (48 ... 57).contains(byte) || byte == 45 || byte == 43 || byte == 46 || byte == 101 || byte == 69
    }
}
