import Foundation

/// Pending responses belong to one authenticated user at one configured API base.
enum AndroidPendingTurnOwnerNamespace {
    static func key(userID: String, baseURL: URL) -> String? {
        guard !userID.isEmpty,
              let origin = AndroidSessionCookieNamespace.key(for: baseURL),
              let components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)
        else { return nil }
        let path = components.percentEncodedPath
        let basePath = if path == "/" {
            ""
        } else if path.hasSuffix("/") {
            String(path.dropLast())
        } else {
            path
        }
        // Length prefixes prevent a delimiter in a user ID or base path from aliasing another owner.
        let originPart = "\(origin.utf8.count):\(origin)"
        let pathPart = "\(basePath.utf8.count):\(basePath)"
        let userPart = "\(userID.utf8.count):\(userID)"
        return "voucha.pending.turn.v1.\(originPart):\(pathPart):\(userPart)"
    }
}
