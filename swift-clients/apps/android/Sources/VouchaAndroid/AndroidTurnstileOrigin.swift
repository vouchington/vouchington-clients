import Foundation

/// The challenge must execute on the configured deployment, without API paths or credentials.
func androidTurnstileOrigin(_ baseURL: URL) -> String? {
    guard var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false),
          let scheme = components.scheme?.lowercased(),
          scheme == "https" || scheme == "http",
          let host = components.host, !host.isEmpty
    else { return nil }
    components.scheme = scheme
    components.host = host.lowercased()
    components.user = nil
    components.password = nil
    components.path = ""
    components.query = nil
    components.fragment = nil
    return components.url?.absoluteString
}
