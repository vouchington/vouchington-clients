/// Email codes use ten ASCII RFC 4648 base32 characters, independently of authenticator TOTP.
public enum EmailVerificationCode {
    public static func normalized(_ input: String) -> String? {
        let bytes = Array(input.utf8)
        guard bytes.count == 10 else { return nil }
        let uppercase = bytes.map { byte in
            (97 ... 122).contains(byte) ? byte - 32 : byte
        }
        guard uppercase.allSatisfy({ (65 ... 90).contains($0) || (50 ... 55).contains($0) }) else {
            return nil
        }
        return String(decoding: uppercase, as: UTF8.self)
    }
}

/// Validates at request encoding, before URLSession can send an email verification request.
public struct EncodedEmailVerificationCode: Encodable, Sendable {
    public enum ValidationError: Error { case invalidFormat }
    private let input: String

    public init(_ input: String) {
        self.input = input
    }

    public func encode(to encoder: any Encoder) throws {
        guard let value = EmailVerificationCode.normalized(input) else {
            throw ValidationError.invalidFormat
        }
        var container = encoder.singleValueContainer()
        try container.encode(value)
    }
}
