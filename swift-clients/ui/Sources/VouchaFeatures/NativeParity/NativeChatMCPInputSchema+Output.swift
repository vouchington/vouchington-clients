import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    /// Output is bounded separately from the much smaller input argument structure.
    static func acceptsOutput(
        _ value: DecodedJSONValue, schema: DecodedJSONValue, root: DecodedJSONValue
    ) throws -> Bool {
        var remainingWork = 262_144
        return try check(value, schema: schema, root: root, depth: 0, remainingWork: &remainingWork)
    }

    static func validDateTime(_ text: String) -> Bool {
        let pattern = #"^([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt\s]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]+)?"# +
            #"(?:([Zz])|([+-])([0-9]{2})(?::?([0-9]{2}))?)\z"#
        guard let regex = try? NSRegularExpression(pattern: pattern),
              let match = regex.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)) else { return false }
        func part(_ index: Int) -> Int {
            guard let range = Range(match.range(at: index), in: text) else { return 0 }
            return Int(text[range]) ?? 0
        }
        let year = part(1), month = part(2), day = part(3)
        let leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0)
        let days = [0, 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
        guard (1 ... 12).contains(month), day >= 1, day <= days[month] else { return false }
        let hour = part(4), minute = part(5), second = part(6), offsetHour = part(9), offsetMinute = part(10)
        guard hour <= 23, minute <= 59, offsetHour <= 23, offsetMinute <= 59, second <= 60 else { return false }
        if second < 60 { return true }
        let signRange = Range(match.range(at: 8), in: text)
        let sign = signRange.map { text[$0] == "-" } == true ? -1 : 1
        let utcMinute = minute - offsetMinute * sign
        let utcHour = hour - offsetHour * sign - (utcMinute < 0 ? 1 : 0)
        return (utcHour == 23 || utcHour == -1) && (utcMinute == 59 || utcMinute == -1)
    }
}
