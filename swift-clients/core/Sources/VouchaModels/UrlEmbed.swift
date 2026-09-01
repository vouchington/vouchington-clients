import Foundation

/// Server-resolved URL presentation data. Raw maps are retained losslessly for
/// future native rendering; presentation must use the server-safe flattened fields.
public struct UrlEmbed: Codable, Sendable {
    public let rssFeedItemId: String?
    public let sourceUrl: String?
    public let title: String?
    public let markdown: String?
    public let thumbnailUrl: String?
    public let playerUrl: String?
    public let playerWidth: Decimal?
    public let playerHeight: Decimal?
    public let mediaType: String?
    public let videoId: String?
    public let videoPlatform: String?
    public let enclosureUrl: String?
    public let enclosureType: String?
    public let durationSeconds: Decimal?
    public let showId: String?
    public let showTitle: String?
    public let showTopicSlug: String?
    public let showTopicType: String?
    public let embedMetadata: DecodedJSONValue?
    public let metaTags: [String: DecodedJSONValue]?
    public let embedOembedUrl: String?
    public let embedOembedResolvedAt: Date?

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(rssFeedItemId, forKey: .rssFeedItemId)
        try container.encode(sourceUrl, forKey: .sourceUrl)
        try container.encode(title, forKey: .title)
        try container.encode(markdown, forKey: .markdown)
        try container.encode(thumbnailUrl, forKey: .thumbnailUrl)
        try container.encode(playerUrl, forKey: .playerUrl)
        try container.encode(playerWidth, forKey: .playerWidth)
        try container.encode(playerHeight, forKey: .playerHeight)
        try container.encode(mediaType, forKey: .mediaType)
        try container.encode(videoId, forKey: .videoId)
        try container.encode(videoPlatform, forKey: .videoPlatform)
        try container.encode(enclosureUrl, forKey: .enclosureUrl)
        try container.encode(enclosureType, forKey: .enclosureType)
        try container.encode(durationSeconds, forKey: .durationSeconds)
        try container.encode(showId, forKey: .showId)
        try container.encode(showTitle, forKey: .showTitle)
        try container.encode(showTopicSlug, forKey: .showTopicSlug)
        try container.encode(showTopicType, forKey: .showTopicType)
        try container.encode(embedMetadata, forKey: .embedMetadata)
        try container.encode(metaTags, forKey: .metaTags)
        try container.encode(embedOembedUrl, forKey: .embedOembedUrl)
        try container.encode(embedOembedResolvedAt, forKey: .embedOembedResolvedAt)
    }

    public var previewTitle: String? {
        firstNonEmpty(
            metadataString("title"),
            metaTag("og:title"),
            metaTag("twitter:title"),
            title
        )
    }

    public var previewDescription: String? {
        firstNonEmpty(
            metadataString("description"),
            metaTag("og:description"),
            metaTag("twitter:description")
        )
    }

    public var previewProvider: String? {
        if case let .object(metadata)? = embedMetadata,
           case let .object(provider)? = metadata["provider"],
           case let .string(name)? = provider["name"],
           let name = Self.trimmedNonEmpty(name) {
            return name
        }
        if let siteName = metaTag("og:site_name") {
            return siteName
        }
        guard let sourceUrl, let host = URL(string: sourceUrl)?.host else { return nil }
        return Self.trimmedNonEmpty(host)
    }

    public var playableProviderURL: URL? {
        guard let playerUrl, let url = URL(string: playerUrl), Self.isAllowedProviderURL(url) else { return nil }
        return url
    }

    public var validatedSourceURL: URL? {
        guard let sourceUrl,
              let url = URL(string: sourceUrl),
              Self.isAllowedSourceURL(url)
        else { return nil }
        return url
    }

    public var approvedPlayerWithSource: (playerURL: URL, sourceURL: URL)? {
        guard let playerURL = playableProviderURL, let sourceURL = validatedSourceURL else { return nil }
        return (playerURL, sourceURL)
    }

    public static func isAllowedProviderURL(_ url: URL) -> Bool {
        guard rawAuthority(of: url) == "www.youtube-nocookie.com" || rawAuthority(of: url) == "player.vimeo.com",
              url.scheme == "https",
              url.user == nil, url.password == nil, url.port == nil,
              let host = url.host
        else { return false }
        switch host {
        case "www.youtube-nocookie.com": return url.path.hasPrefix("/embed/")
        case "player.vimeo.com": return url.path.hasPrefix("/video/")
        default: return false
        }
    }

    public static func isAllowedSourceURL(_ url: URL) -> Bool {
        url.scheme == "https" && url.user == nil && url.password == nil && url
            .port == nil && rawAuthority(of: url) != nil
    }

    private func metadataString(_ key: String) -> String? {
        guard case let .object(metadata)? = embedMetadata,
              case let .string(value)? = metadata[key]
        else { return nil }
        return Self.trimmedNonEmpty(value)
    }

    private func metaTag(_ key: String) -> String? {
        guard let value = metaTags?.first(where: { $0.key.caseInsensitiveCompare(key) == .orderedSame })?.value,
              case let .string(string) = value
        else { return nil }
        return Self.trimmedNonEmpty(string)
    }

    private func firstNonEmpty(_ values: String?...) -> String? {
        values.compactMap { Self.trimmedNonEmpty($0) }.first
    }

    private static func trimmedNonEmpty(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func rawAuthority(of url: URL) -> String? {
        guard let schemeRange = url.absoluteString.range(of: "://"),
              url.absoluteString[..<schemeRange.lowerBound] == "https"
        else { return nil }
        let remainder = url.absoluteString[schemeRange.upperBound...]
        return remainder.split(whereSeparator: { $0 == "/" || $0 == "?" || $0 == "#" }).first.map(String.init)
    }
}
