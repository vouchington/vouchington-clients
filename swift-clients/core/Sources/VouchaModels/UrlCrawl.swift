import Foundation

public struct UrlCrawl: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let responseStatusCode: Int?
    public let createdAt: Date?
    public let completedAt: Date?
    public let lang: String?
    public let markdown: String?
    public let metaTags: [String: String]?
    public let title: String?
    public let urlId: String?
    public let crawlerId: String?
    public let embeddingsGeneratedAt: Date?
    public let etag: String?
    public let hasPendingEmbeddings: Bool?
    public let htmlSha256: DecodedJSONValue?
    public let htmlSnapshotUploadedAt: Date?
    public let lastModifiedAt: Date?
    public let links: [String: DecodedJSONValue]?
    public let networkError: String?
    public let redirectUrlId: String?
    public let requestHeaders: [String: DecodedJSONValue]?
    public let responseHeaders: [String: DecodedJSONValue]?
    private let encodedFields: Set<CodingKeys>

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
        case responseStatusCode
        case createdAt
        case completedAt
        case lang
        case markdown
        case metaTags
        case title
        case urlId
        case crawlerId, embeddingsGeneratedAt, etag, hasPendingEmbeddings, htmlSha256
        case htmlSnapshotUploadedAt, lastModifiedAt, links, networkError, redirectUrlId
        case requestHeaders, responseHeaders
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        encodedFields = Set(container.allKeys)
        entityType = try container.decodeIfPresent(String.self, forKey: .entityType)
        id = try container.decode(String.self, forKey: .id)
        responseStatusCode = try container.decodeIfPresent(Int.self, forKey: .responseStatusCode)
        createdAt = try container.decodeIfPresent(Date.self, forKey: .createdAt)
        completedAt = try container.decodeIfPresent(Date.self, forKey: .completedAt)
        lang = try container.decodeIfPresent(String.self, forKey: .lang)
        markdown = try container.decodeIfPresent(String.self, forKey: .markdown)
        metaTags = try container.decodeIfPresent([String: String].self, forKey: .metaTags)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        urlId = try container.decodeIfPresent(String.self, forKey: .urlId)
        crawlerId = try container.decodeIfPresent(String.self, forKey: .crawlerId)
        embeddingsGeneratedAt = try container.decodeIfPresent(Date.self, forKey: .embeddingsGeneratedAt)
        etag = try container.decodeIfPresent(String.self, forKey: .etag)
        hasPendingEmbeddings = try container.decodeIfPresent(Bool.self, forKey: .hasPendingEmbeddings)
        htmlSha256 = try container.decodeIfPresent(DecodedJSONValue.self, forKey: .htmlSha256)
        htmlSnapshotUploadedAt = try container.decodeIfPresent(Date.self, forKey: .htmlSnapshotUploadedAt)
        lastModifiedAt = try container.decodeIfPresent(Date.self, forKey: .lastModifiedAt)
        links = try container.decodeIfPresent([String: DecodedJSONValue].self, forKey: .links)
        networkError = try container.decodeIfPresent(String.self, forKey: .networkError)
        redirectUrlId = try container.decodeIfPresent(String.self, forKey: .redirectUrlId)
        requestHeaders = try container.decodeIfPresent(
            [String: DecodedJSONValue].self,
            forKey: .requestHeaders
        )
        responseHeaders = try container.decodeIfPresent(
            [String: DecodedJSONValue].self,
            forKey: .responseHeaders
        )
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try encode(entityType, forKey: .entityType, to: &container)
        try container.encode(id, forKey: .id)
        try encode(responseStatusCode, forKey: .responseStatusCode, to: &container)
        try encode(createdAt, forKey: .createdAt, to: &container)
        try encode(completedAt, forKey: .completedAt, to: &container)
        try encode(lang, forKey: .lang, to: &container)
        try encode(markdown, forKey: .markdown, to: &container)
        try encode(metaTags, forKey: .metaTags, to: &container)
        try encode(title, forKey: .title, to: &container)
        try encode(urlId, forKey: .urlId, to: &container)
        try encode(crawlerId, forKey: .crawlerId, to: &container)
        try encode(embeddingsGeneratedAt, forKey: .embeddingsGeneratedAt, to: &container)
        try encode(etag, forKey: .etag, to: &container)
        try encode(hasPendingEmbeddings, forKey: .hasPendingEmbeddings, to: &container)
        try encode(htmlSha256, forKey: .htmlSha256, to: &container)
        try encode(htmlSnapshotUploadedAt, forKey: .htmlSnapshotUploadedAt, to: &container)
        try encode(lastModifiedAt, forKey: .lastModifiedAt, to: &container)
        try encode(links, forKey: .links, to: &container)
        try encode(networkError, forKey: .networkError, to: &container)
        try encode(redirectUrlId, forKey: .redirectUrlId, to: &container)
        try encode(requestHeaders, forKey: .requestHeaders, to: &container)
        try encode(responseHeaders, forKey: .responseHeaders, to: &container)
    }

    private func encode(
        _ value: (some Encodable)?,
        forKey key: CodingKeys,
        to container: inout KeyedEncodingContainer<CodingKeys>
    ) throws {
        guard encodedFields.contains(key) else { return }
        try container.encode(value, forKey: key)
    }
}
