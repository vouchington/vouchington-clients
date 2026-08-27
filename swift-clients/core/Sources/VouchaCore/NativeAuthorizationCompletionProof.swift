import Crypto
import Foundation
#if canImport(Security)
    import Security
#endif

public struct NativeAuthorizationCompletionProof: Equatable, Sendable {
    public let verifier: String
    public let challenge: String

    public static func generate() throws -> NativeAuthorizationCompletionProof {
        var bytes = [UInt8](repeating: 0, count: 32)
        #if canImport(Security)
            guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else {
                throw NativeAuthorizationCompletionProofError.randomGenerationFailed
            }
        #else
            var generator = SystemRandomNumberGenerator()
            for index in bytes.indices {
                bytes[index] = UInt8.random(in: .min ... .max, using: &generator)
            }
        #endif
        return fromVerifier(Data(bytes).base64URLEncodedString())
    }

    public static func fromVerifier(_ verifier: String) -> NativeAuthorizationCompletionProof {
        let digest = SHA256.hash(data: Data(verifier.utf8))
        return NativeAuthorizationCompletionProof(
            verifier: verifier,
            challenge: Data(digest).base64URLEncodedString()
        )
    }
}

public enum NativeAuthorizationCompletionProofError: Error {
    case randomGenerationFailed
}
