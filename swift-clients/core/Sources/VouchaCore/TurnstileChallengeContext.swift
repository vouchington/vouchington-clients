import Foundation

/// Provider context shared by the Apple and Android challenge renderers.
public struct TurnstileChallengeContext: Hashable, Sendable {
    public let action: String
    public let origin: URL

    public init?(actionTag: String, baseURL: URL) {
        let action = actionTag.replacingOccurrences(of: ".", with: "-")
        guard (1 ... 32).contains(action.utf8.count), action.utf8.allSatisfy({
            (65 ... 90).contains($0) || (97 ... 122).contains($0) ||
                (48 ... 57).contains($0) || $0 == 95 || $0 == 45
        }), var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false),
        let scheme = components.scheme?.lowercased(), scheme == "https" || scheme == "http",
        let host = components.host, !host.isEmpty else { return nil }
        components.scheme = scheme
        components.host = host.lowercased()
        components.user = nil
        components.password = nil
        components.path = ""
        components.query = nil
        components.fragment = nil
        guard let origin = components.url else { return nil }
        self.action = action
        self.origin = origin
    }

    public func widgetAttributes(siteKey: String) -> String {
        let escaped = siteKey.replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "\"", with: "&quot;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "'", with: "&#39;")
        return "data-sitekey=\"\(escaped)\" data-action=\"\(action)\""
    }
}
