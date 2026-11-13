import VouchaAPI
import VouchaModels

/// Reads the backend Hacker News reference flag (`/api/v1/hacker-news-config`).
enum HackerNewsReferencesVisibility {
    /// `nil` when the request fails or is cancelled, so callers keep the last known value and the
    /// filter stays hidden until a request has succeeded.
    static func fetch(client: APIClient) async -> Bool? {
        let config: HackerNewsConfigResponse? = try? await client.send(Endpoint.hackerNewsConfig)
        return config?.referencesVisible
    }
}
