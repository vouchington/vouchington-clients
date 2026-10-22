import Foundation

public enum UuidV7 {
    public static func nextSequentialPair(at date: Date = Date()) -> (String, String) {
        let milliseconds = UInt64(date.timeIntervalSince1970 * 1_000)
        return (string(milliseconds: milliseconds), string(milliseconds: milliseconds + 1))
    }

    public static func string(milliseconds: UInt64) -> String {
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
