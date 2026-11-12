import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

public extension APIClient {
    /// Installs the native presentation boundary. Only explicit server mutation challenges invoke it.
    func setMutationChallengeHandler(_ handler: (@Sendable () async throws -> String?)?) {
        mutationChallengeHandler = handler
    }
}

extension APIClient {
    func mutationData(for rawRequest: URLRequest, endpoint: Endpoint) async throws -> (Data, URLResponse) {
        let method = rawRequest.httpMethod ?? "GET"
        let sessionIdentity = mutationSessionIdentity()
        let signed = await applySigningIfNeeded(rawRequest, method: method)
        let first = try await session.data(for: signed)
        guard !isProductSignal(endpoint), rawRequest.url?.scheme == baseURL.scheme,
              rawRequest.url?.host == baseURL.host, rawRequest.url?.port == baseURL.port,
              rawRequest.httpBodyStream == nil, ["POST", "PUT", "PATCH", "DELETE"].contains(method),
              let response = first.1 as? HTTPURLResponse, response.statusCode == 403,
              response.url == rawRequest.url,
              let challenge = try? decoder.decode(BotChallengeRequiredResponse.self, from: first.0),
              challenge.code == BotChallengeRequiredResponse.requiredCode,
              challenge.action == "bot-challenge.mutation",
              let handler = mutationChallengeHandler else { return first }
        try Task.checkCancellation()
        guard let token = try await handler(), !token.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              mutationSessionIdentity() == sessionIdentity else { return first }
        try Task.checkCancellation()
        // Keep the original encoded bytes and route proof; refresh normal request signing only.
        var replay = await applySigningIfNeeded(rawRequest, method: method)
        replay.setValue(token, forHTTPHeaderField: "x-voucha-bot-challenge-token")
        try Task.checkCancellation()
        guard mutationSessionIdentity() == sessionIdentity else { return first }
        return try await session.data(for: replay)
    }

    private func isProductSignal(_ endpoint: Endpoint) -> Bool {
        let parts = endpoint.path.split(separator: "/")
        guard parts.count == 5, parts[0] == "api", parts[1] == "v1" else { return false }
        switch (parts[2], parts[4]) {
        case ("landing-pages", "visits"), ("landing-pages", "clicks"),
             ("rss-feeds", "views"), ("rss-feed-items", "views"): return true
        default: return false
        }
    }

    private func mutationSessionIdentity() -> [String] {
        (cookieStorage.cookies(for: baseURL) ?? []).filter { $0.name == "st" || $0.name == "dt" }
            .map { "\($0.domain)|\($0.path)|\($0.value)" }.sorted()
    }
}
