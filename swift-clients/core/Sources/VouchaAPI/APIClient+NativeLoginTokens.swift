import Foundation
import VouchaCore
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public extension APIClient {
    /// Installs the `dt`/`st` body tokens a native email login returns through the same coordinated
    /// cookie commit as `Set-Cookie` credentials, so the following session refresh can use them.
    /// Tokens already present in the jar (for example from `Set-Cookie`) are left untouched; new
    /// ones take their expiry from the signed JWT `exp` claim and fail closed without one.
    func installNativeLoginTokens(deviceToken: String, sessionToken: String) throws {
        let stamp = cookieCoordinator.stamp
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
            if current == nil, baseURL.scheme == "https" { properties[.secure] = "TRUE" }
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

    internal static func jwtExpiration(_ token: String) -> Date? {
        let parts = token.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, let data = try? Data(base64URLEncoded: String(parts[1])),
              let claims = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let exp = (claims["exp"] as? NSNumber)?.doubleValue else { return nil }
        return Date(timeIntervalSince1970: exp)
    }
}
