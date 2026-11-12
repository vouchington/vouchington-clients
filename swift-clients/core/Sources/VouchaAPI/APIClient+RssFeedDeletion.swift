import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaCore

/// Acceptance queues deletion; callers must retain the feed until later authoritative data changes.
public enum RssFeedDeletionOutcome: Equatable, Sendable {
    case accepted(rssFeedId: String)
    case blockedByAdminImports

    public var status: String? {
        switch self {
        case .accepted: "deleting"
        case .blockedByAdminImports: nil
        }
    }
}

private struct RssFeedDeletionAcceptance: Decodable {
    enum Status: String, Decodable { case deleting }
    let rssFeedId: String
    let status: Status
}

private struct RssFeedDeletionConflict: Decodable {
    let code: String
}

public extension APIClient {
    func deleteRssFeed(rssFeedId: String) async throws -> RssFeedDeletionOutcome {
        try await ensureSessionBootstrap()
        defer { persistCookies?() }
        let endpoint = Endpoint.deleteRssFeed(rssFeedId: rssFeedId)
        let request = try buildRequest(endpoint)
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await mutationData(for: request, endpoint: endpoint)
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        if status == 409,
           let conflict = try? decoder.decode(RssFeedDeletionConflict.self, from: data),
           conflict.code == "RSS_FEED_ADMIN_IMPORT_REFERENCED" {
            return .blockedByAdminImports
        }
        try validate(response: response, data: data)
        guard status == 202 else { throw VouchaError.api(statusCode: status, preconditionCode: nil) }
        do {
            let accepted = try decoder.decode(RssFeedDeletionAcceptance.self, from: data)
            return .accepted(rssFeedId: accepted.rssFeedId)
        } catch let error as DecodingError {
            throw VouchaError.decodingFailed(error)
        }
    }
}
