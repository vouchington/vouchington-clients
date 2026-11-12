import Foundation
import VouchaAPI
import VouchaModels

struct SourceCreationService {
    var create: @Sendable (String) async throws -> RssFeedImportSubmission
    var status: @Sendable (String) async throws -> RssFeedImportStatus
    var topic: @Sendable (String) async throws -> String?

    static func live(client: APIClient) -> Self {
        Self(
            create: { try await client.send(.createSource(rssFeedURL: $0, follow: true)) },
            status: { try await client.send(.rssFeedImportStatus(importId: $0)) },
            topic: { id in
                let detail: RssFeedDetailResponse = try await client.send(.rssFeed(id: id))
                return detail.rssFeed.id == id ? detail.rssFeed.topicId : nil
            }
        )
    }

    static func validStatusURL(_ value: String, importId: String, baseURL: URL) -> Bool {
        guard !importId.isEmpty, importId.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "-") }),
              let supplied = URLComponents(string: value),
              supplied.user == nil, supplied.password == nil,
              supplied.query == nil, supplied.fragment == nil,
              supplied.percentEncodedPath == "/api/v1/my/import/rss-feeds/\(importId)"
        else { return false }
        if supplied.scheme == nil {
            return supplied.host == nil && value.hasPrefix("/") && !value.hasPrefix("//")
        }
        guard let base = URLComponents(url: baseURL, resolvingAgainstBaseURL: false) else { return false }
        return supplied.scheme?.lowercased() == base.scheme?.lowercased()
            && supplied.host?.lowercased() == base.host?.lowercased()
            && effectivePort(supplied) == effectivePort(base)
    }

    private static func effectivePort(_ value: URLComponents) -> Int? {
        value.port ?? (value.scheme?.lowercased() == "https" ? 443 : 80)
    }
}
