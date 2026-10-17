import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

/// Keeps URLSession cookies in its ephemeral jar and persists snapshots only through SkipKeychain.
final class AndroidSessionCookieStorage: @unchecked Sendable {
    struct CookieRecord: Codable {
        var name: String
        var value: String
        var domain: String
        var hostOnly: Bool
        var path: String
        var expires: Date?
        var secure: Bool
        var httpOnly: Bool

        init?(cookie: HTTPCookie) {
            name = cookie.name
            value = cookie.value
            domain = cookie.domain
            hostOnly = !cookie.domain.hasPrefix(".")
            path = cookie.path
            // HTTPCookie resolves Max-Age against Expires when it is created and exposes
            // that absolute deadline as expiresDate. Keep it unchanged across snapshots.
            expires = cookie.expiresDate
            secure = cookie.isSecure
            httpOnly = cookie.isHTTPOnly
        }

        var cookie: HTTPCookie? {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: name,
                .value: value,
                .domain: domain,
                .path: path
            ]
            if let expires { properties[.expires] = expires }
            if !hostOnly, !domain.hasPrefix(".") { properties[.domain] = ".\(domain)" }
            if secure { properties[.secure] = "TRUE" }
            if httpOnly { properties[HTTPCookiePropertyKey("HttpOnly")] = "TRUE" }
            return HTTPCookie(properties: properties)
        }
    }

    let sessionConfiguration: URLSessionConfiguration
    let cookieStorage: HTTPCookieStorage
    private let secrets: any AndroidEndpointSecretStoring
    private let key: String
    private let queue = DispatchQueue(label: "ai.voucha.android.session-cookies")
    private var persistenceEnabled = true

    init(
        secrets: any AndroidEndpointSecretStoring = SkipKeychainEndpointSecretStore(),
        key: String = "voucha.session.cookies"
    ) {
        let configuration = URLSessionConfiguration.ephemeral
        guard let cookieStorage = configuration.httpCookieStorage else {
            preconditionFailure("Android authentication requires URLSession's ephemeral cookie store")
        }
        sessionConfiguration = configuration
        self.cookieStorage = cookieStorage
        self.secrets = secrets
        self.key = key
        restoreCookies()
    }

    func persistCookies() {
        queue.sync {
            guard persistenceEnabled else { return }
            let records = (cookieStorage.cookies ?? []).compactMap(CookieRecord.init(cookie:))
                .filter { $0.expires.map { $0 > Date() } ?? true }
            guard let data = try? JSONEncoder().encode(records),
                  let json = String(data: data, encoding: .utf8) else { return }
            try? secrets.set(json, forKey: key)
        }
    }

    var cookies: [HTTPCookie]? {
        cookieStorage.cookies
    }

    func cookies(for url: URL) -> [HTTPCookie]? {
        cookieStorage.cookies(for: url)
    }

    func setCookie(_ cookie: HTTPCookie) {
        cookieStorage.setCookie(cookie)
        persistCookies()
    }

    func deleteCookie(_ cookie: HTTPCookie) {
        cookieStorage.deleteCookie(cookie)
        persistCookies()
    }

    /// Permanently fences this client's callbacks before logout or account replacement.
    func disablePersistenceWrites() {
        queue.sync { persistenceEnabled = false }
    }

    func clearPersistedCookies() {
        queue.sync {
            persistenceEnabled = false
            try? secrets.removeValue(forKey: key)
        }
    }

    private func restoreCookies() {
        guard let json = try? secrets.string(forKey: key), let data = json.data(using: .utf8),
              let records = try? JSONDecoder().decode([CookieRecord].self, from: data) else { return }
        for record in records where record.expires.map({ $0 > Date() }) ?? true {
            if let cookie = record.cookie {
                cookieStorage.setCookie(cookie)
            }
        }
    }
}
