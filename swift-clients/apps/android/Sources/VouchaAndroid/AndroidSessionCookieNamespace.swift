import Foundation

/// Snapshot identity follows the configured API origin, independently of cookie-domain matching.
enum AndroidSessionCookieNamespace {
    static func key(for baseURL: URL) -> String? {
        guard let components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false),
              let scheme = components.scheme?.lowercased(),
              scheme == "http" || scheme == "https",
              let host = components.host?.lowercased(), !host.isEmpty
        else { return nil }
        let port = components.port ?? (scheme == "https" ? 443 : 80)
        guard (1 ... 65_535).contains(port) else { return nil }
        return "voucha.session.cookies.\(scheme)://\(host):\(port)"
    }
}
