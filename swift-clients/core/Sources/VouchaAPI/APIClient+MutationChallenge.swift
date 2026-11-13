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
        let initialRevision = cookieCoordinator.stamp.revision
        let first = try await executeBuffered(rawRequest, endpoint: endpoint, mayRecover: true)
        guard !isProductSignal(endpoint), rawRequest.url?.scheme == baseURL.scheme,
              rawRequest.url?.host == baseURL.host, rawRequest.url?.port == baseURL.port,
              rawRequest.httpBodyStream == nil, ["POST", "PUT", "PATCH", "DELETE"].contains(method),
              rawRequest.value(forHTTPHeaderField: RequestSignatureHeader.challengeId) == nil,
              rawRequest.value(forHTTPHeaderField: RequestSignatureHeader.assertion) == nil,
              let response = first.1 as? HTTPURLResponse, response.statusCode == 403,
              response.url == rawRequest.url,
              let challenge = try? decoder.decode(BotChallengeRequiredResponse.self, from: first.0),
              challenge.code == BotChallengeRequiredResponse.requiredCode,
              challenge.action == "bot-challenge.mutation",
              let handler = mutationChallengeHandler else { return first }
        // Recovery may rotate credentials before returning the first challenge. Bind
        // its proof to that current owner, then fence account changes while prompting.
        let owner = cookieCoordinator.stamp
        let sessionIdentity = mutationSessionIdentity()
        try Task.checkCancellation()
        guard let token = try await handler(), !token.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              mutationSessionIdentity() == sessionIdentity else { return first }
        try Task.checkCancellation()
        try cookieCoordinator.check(owner)
        // Keep original encoded bytes and route proof. Each buffered attempt prepares
        // current credentials and fresh signing, including a subsequent 401 recovery.
        var replay = rawRequest
        replay.setValue(token, forHTTPHeaderField: "x-voucha-bot-challenge-token")
        try Task.checkCancellation()
        guard mutationSessionIdentity() == sessionIdentity else { return first }
        return try await executeBuffered(
            replay, endpoint: endpoint, mayRecover: owner.revision == initialRevision, expectedEpoch: owner.epoch
        )
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
