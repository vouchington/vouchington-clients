import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension AndroidSessionCookieStorage {
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
            let properties = cookie.properties ?? [:]
            if let maximumAge = properties[.maximumAge] {
                guard let age = Self.timeInterval(maximumAge), age > 0,
                      let created = Self.date(properties[HTTPCookiePropertyKey("Created")])
                else { return nil }
                // FoundationNetworking 6.3.3 only resolves Max-Age for version-1 cookies.
                // Use the cookie's original Created timestamp so subsequent snapshots cannot
                // extend its lifetime; Max-Age takes precedence over Expires.
                expires = created.addingTimeInterval(age)
            } else {
                expires = cookie.expiresDate
            }
            secure = cookie.isSecure
            httpOnly = cookie.isHTTPOnly
        }

        private static func timeInterval(_ value: Any) -> TimeInterval? {
            if let value = value as? String { return TimeInterval(value) }
            if let value = value as? NSNumber { return value.doubleValue }
            return nil
        }

        private static func date(_ value: Any?) -> Date? {
            if let value = value as? Date { return value }
            guard let seconds = value as? NSNumber else { return nil }
            return Date(timeIntervalSinceReferenceDate: seconds.doubleValue)
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

}
