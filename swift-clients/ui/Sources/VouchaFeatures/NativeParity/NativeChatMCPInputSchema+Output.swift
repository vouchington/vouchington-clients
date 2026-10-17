import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    /// Resolves the single local JSON Pointer form supported by output schemas.
    /// Raw slashes delimit pointer tokens; only escaped slashes belong to a key.
    static func localDefinitionName(from path: String) -> String? {
        guard path.hasPrefix("#/$defs/") else { return nil }
        let token = path.dropFirst("#/$defs/".count)
        guard !token.contains("/") else { return nil }
        let scalars = Array(token.unicodeScalars)
        var decoded = String.UnicodeScalarView()
        var index = 0
        while index < scalars.count {
            guard scalars[index] == "~" else {
                decoded.append(scalars[index])
                index += 1
                continue
            }
            guard index + 1 < scalars.count else { return nil }
            switch scalars[index + 1] {
            case "0": decoded.append("~")
            case "1": decoded.append("/")
            default: return nil
            }
            index += 2
        }
        return String(decoded)
    }

    /// Output is bounded separately from the much smaller input argument structure.
    static func acceptsOutput(
        _ value: DecodedJSONValue, schema: DecodedJSONValue, root: DecodedJSONValue,
        selectedSchema: DecodedJSONValue? = nil
    ) throws -> Bool {
        var remainingWork = 262_144
        guard try check(
            value, schema: schema, root: root, depth: 0, remainingWork: &remainingWork
        ) else { return false }
        guard let selectedSchema else { return true }
        return try check(value, schema: selectedSchema, root: root, depth: 0, remainingWork: &remainingWork)
    }

    static func validDateTime(_ text: String) -> Bool {
        let pattern = #"^([0-9]{4})-([0-9]{2})-([0-9]{2})"# +
            #"[Tt\x09-\x0D\x20\xA0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]"# +
            #"([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]+)?"# +
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
