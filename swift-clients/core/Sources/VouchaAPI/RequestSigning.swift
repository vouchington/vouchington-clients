import Crypto
import Foundation

/// Protocol for per-request signing. Returning `nil` means "don't sign this request" (passthrough).
public protocol RequestSigning: Sendable {
    func signingHeaders(
        method: String,
        path: String,
        body: Data?
    ) async throws -> [String: String]?
}

/// HTTP header name constants for App Attest per-request and gated-flow signing.
public enum RequestSignatureHeader {
    public static let keyId = "x-app-attest-key-id"
    public static let assertion = "x-app-attest-assertion"
    public static let timestamp = "x-app-attest-timestamp"
    public static let nonce = "x-app-attest-nonce"
    /// Presence of this header means the request is in the gated-flow path — skip per-request signing.
    public static let challengeId = "x-app-attest-challenge-id"
}

/// Canonical request string builder. Must match the backend `request-canonical.mts` byte-for-byte.
///
/// Format — 5 lines joined by `\n`, no trailing newline:
/// ```
/// VOUCHA-REQSIG-v1
/// {METHOD uppercased}
/// {PATH — pathname only, no query string}
/// {lowercase hex SHA-256 of raw body bytes; empty body → emptyBodyHash}
/// {Unix epoch seconds}
/// {nonce}
/// ```
public enum CanonicalRequestString {
    /// SHA-256 of empty data, lowercase hex.
    public static let emptyBodyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"

    public static func build(
        method: String,
        path: String,
        bodyHash: String,
        timestamp: Int,
        nonce: String
    ) -> String {
        [
            "VOUCHA-REQSIG-v1",
            method.uppercased(),
            path,
            bodyHash,
            String(timestamp),
            nonce
        ].joined(separator: "\n")
    }

    /// Returns lowercase hex SHA-256 of `data`, or `emptyBodyHash` if nil/empty.
    public static func bodyHash(from data: Data?) -> String {
        guard let data, !data.isEmpty else { return emptyBodyHash }
        return SHA256.hash(data: data)
            .compactMap { String(format: "%02x", $0) }.joined()
    }
}
