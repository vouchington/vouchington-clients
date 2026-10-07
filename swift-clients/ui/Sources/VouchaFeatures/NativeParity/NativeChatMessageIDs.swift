import Foundation

enum NativeChatMessageIDs {
    static func nextLocalTurn() -> (user: String, assistant: String) {
        let milliseconds = UInt64(Date().timeIntervalSince1970 * 1_000)
        return (uuidV7(milliseconds: milliseconds), uuidV7(milliseconds: milliseconds + 1))
    }

    private static func uuidV7(milliseconds: UInt64) -> String {
        var bytes = (0 ..< 16).map { _ in UInt8.random(in: .min ... .max) }
        for index in 0 ..< 6 {
            bytes[index] = UInt8(truncatingIfNeeded: milliseconds >> (8 * (5 - index)))
        }
        bytes[6] = (bytes[6] & 0x0F) | 0x70
        bytes[8] = (bytes[8] & 0x3F) | 0x80
        let digits = Array("0123456789abcdef")
        let hex = bytes.map { byte in
            String([digits[Int(byte >> 4)], digits[Int(byte & 0x0F)]])
        }
        return [
            hex[0 ... 3].joined(), hex[4 ... 5].joined(), hex[6 ... 7].joined(),
            hex[8 ... 9].joined(), hex[10 ... 15].joined()
        ].joined(separator: "-")
    }
}
