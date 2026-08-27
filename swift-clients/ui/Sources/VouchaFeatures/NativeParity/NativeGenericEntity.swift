struct NativeGenericEntity: Decodable {
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

    private enum CodingKeys: String, CodingKey {
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
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
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
    }

    init(id: String) {
        self.id = id
        slug = nil
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
        summary: String?
    ) {
        self.id = id
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
    }

    func displayTitle(fallback: String) -> String {
        title ?? name ?? subject ?? username ?? hostname?.displayName ?? url ?? slug ?? id.ifNotEmpty ?? fallback
    }

    var displayDetail: String {
        description ?? summary ?? status ?? topicType ?? postType ?? feedType ?? pathname ?? slug ?? id
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
