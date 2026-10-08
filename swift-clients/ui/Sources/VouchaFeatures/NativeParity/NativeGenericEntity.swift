import Foundation
import VouchaModels

struct NativeGenericEntity: Decodable {
    let provenance: PublicContentProvenance?
    let id: String
    let slug: String?
    let name: String?
    let title: String?
    let username: String?
    let subject: String?
    let status: String?
    let postType: String?
    let topicType: String?
    let feedType: String?
    let pathname: String?
    let hostname: NativeGenericHostname?
    let url: String?
    let description: String?
    let summary: String?
    let declaredLanguage: String?
    let linguaRsDetectedLanguage: String?

    private enum CodingKeys: String, CodingKey {
        case provenance
        case id
        case entityId
        case slug
        case name
        case title
        case username
        case subject
        case status
        case postType
        case topicType
        case feedType
        case pathname
        case hostname
        case url
        case description
        case summary
        case declaredLanguage
        case linguaRsDetectedLanguage
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        provenance = try container.decodeIfPresent(PublicContentProvenance.self, forKey: .provenance)
        id = try container.decodeIfPresent(String.self, forKey: .id) ?? container.decode(String.self, forKey: .entityId)
        slug = try container.decodeIfPresent(String.self, forKey: .slug)
        name = try container.decodeIfPresent(String.self, forKey: .name)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        username = try container.decodeIfPresent(String.self, forKey: .username)
        subject = try container.decodeIfPresent(String.self, forKey: .subject)
        status = try container.decodeIfPresent(String.self, forKey: .status)
        postType = try container.decodeIfPresent(String.self, forKey: .postType)
        topicType = try container.decodeIfPresent(String.self, forKey: .topicType)
        feedType = try container.decodeIfPresent(String.self, forKey: .feedType)
        pathname = try container.decodeIfPresent(String.self, forKey: .pathname)
        hostname = try container.decodeIfPresent(NativeGenericHostname.self, forKey: .hostname)
        url = try container.decodeIfPresent(String.self, forKey: .url)
        description = try container.decodeIfPresent(String.self, forKey: .description)
        summary = try container.decodeIfPresent(String.self, forKey: .summary)
        declaredLanguage = try container.decodeIfPresent(String.self, forKey: .declaredLanguage)
        linguaRsDetectedLanguage = try container.decodeIfPresent(String.self, forKey: .linguaRsDetectedLanguage)
    }

    init(id: String) {
        self.id = id
        slug = nil
        provenance = nil
        name = nil
        title = nil
        username = nil
        subject = nil
        status = nil
        postType = nil
        topicType = nil
        feedType = nil
        pathname = nil
        hostname = nil
        url = nil
        description = nil
        summary = nil
        declaredLanguage = nil
        linguaRsDetectedLanguage = nil
    }

    init(
        id: String,
        slug: String?,
        name: String?,
        title: String?,
        username: String?,
        subject: String?,
        status: String?,
        postType: String?,
        topicType: String?,
        feedType: String?,
        pathname: String?,
        hostname: NativeGenericHostname?,
        url: String?,
        description: String?,
        summary: String?,
        declaredLanguage: String? = nil,
        linguaRsDetectedLanguage: String? = nil,
        provenance: PublicContentProvenance? = nil
    ) {
        self.id = id
        self.provenance = provenance
        self.slug = slug
        self.name = name
        self.title = title
        self.username = username
        self.subject = subject
        self.status = status
        self.postType = postType
        self.topicType = topicType
        self.feedType = feedType
        self.pathname = pathname
        self.hostname = hostname
        self.url = url
        self.description = description
        self.summary = summary
        self.declaredLanguage = declaredLanguage
        self.linguaRsDetectedLanguage = linguaRsDetectedLanguage
    }

    func displayTitle(fallback: String) -> String {
        normalizedAuthoredTitle?.value
            ?? firstNonBlank(name, subject, username, hostname?.displayName, url, slug, id, fallback)
    }

    var normalizedAuthoredTitle: NormalizedAuthoredText? {
        NormalizedAuthoredText(
            text: title,
            declaredLanguage: declaredLanguage,
            detectedLanguage: linguaRsDetectedLanguage
        )
    }

    var displayDetail: String {
        firstNonBlank(description, summary, status, topicType, postType, feedType, pathname, slug, id)
    }

    private func firstNonBlank(_ values: String?...) -> String {
        values.lazy.compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first(where: { !$0.isEmpty }) ?? ""
    }
}

enum NativeGenericHostname: Decodable {
    case string(String)
    case object(String)

    init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let value = try? container.decode(String.self) {
            self = .string(value)
            return
        }
        let object = try container.decode(NativeHostnameName.self)
        self = .object(object.hostname)
    }

    var displayName: String {
        switch self {
        case let .string(value), let .object(value):
            value
        }
    }
}
