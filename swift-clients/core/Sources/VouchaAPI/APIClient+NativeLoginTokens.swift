import Foundation
import VouchaCore
import VouchaModels
#if !canImport(Darwin)
    import FoundationNetworking
#endif

private struct NativeLoginBodyTokens: Decodable {
    let deviceToken: NativeSessionToken?
    let sessionToken: NativeSessionToken?

    private enum CodingKeys: String, CodingKey {
        case deviceToken = "dt"
        case sessionToken = "st"
    }
}

extension APIClient {
    static let nativeEmailLoginPath = "/api/v1/auth/native/email-address/login"

    /// Installs the `dt`/`st` body tokens a successful native email login returns through the same
    /// coordinated cookie commit as `Set-Cookie` credentials, under the stamp captured when the
    /// request was dispatched, so a sign-out or newer authentication that superseded the request
    /// rejects the install. Tokens already in the jar (for example from `Set-Cookie`) are left
    /// untouched; new ones take their expiry from the signed JWT `exp` claim and fail closed without
    /// one. Bodies without credentials (MFA challenges) are ignored.
    func installNativeLoginTokens(from data: Data, stamp: SessionCookieCoordinator.Stamp) throws {
        guard let tokens = try? decoder.decode(NativeLoginBodyTokens.self, from: data),
              let deviceToken = tokens.deviceToken?.token, let sessionToken = tokens.sessionToken?.token
        else { return }
        let existing = cookieStorage.cookies(for: baseURL) ?? []
        var cookies: [HTTPCookie] = []
        for (name, value) in [("dt", deviceToken), ("st", sessionToken)] {
            let current = existing.first { $0.name == name }
            if current?.value == value { continue }
            guard !value.isEmpty, let expires = Self.jwtExpiration(value), expires > Date() else {
                throw VouchaError.unauthorized
            }
            var properties: [HTTPCookiePropertyKey: Any] = current?.properties ?? [
                .originURL: baseURL, .path: "/", HTTPCookiePropertyKey("HttpOnly"): "TRUE"
            ]
            // Credentials are always HttpOnly, and Secure over HTTPS, even when replacing a cookie that
            // an earlier response stored with weaker attributes.
            properties[HTTPCookiePropertyKey("HttpOnly")] = "TRUE"
            if baseURL.scheme == "https" { properties[.secure] = "TRUE" }
            properties[.name] = name
            properties[.value] = value
            properties[.expires] = expires
            properties.removeValue(forKey: .maximumAge)
            guard let cookie = HTTPCookie(properties: properties) else { throw VouchaError.unauthorized }
            cookies.append(cookie)
        }
        guard !cookies.isEmpty else { return }
        try cookieCoordinator.commit(
            cookies,
            stamp: stamp,
            persist: checkedPersistence,
            clearPersistence: checkedClearPersistence,
            establishingSession: true
        )
    }

    static func jwtExpiration(_ token: String) -> Date? {
        let parts = token.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, let data = try? Data(base64URLEncoded: String(parts[1])),
              let claims = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let exp = (claims["exp"] as? NSNumber)?.doubleValue else { return nil }
        return Date(timeIntervalSince1970: exp)
    }
}
