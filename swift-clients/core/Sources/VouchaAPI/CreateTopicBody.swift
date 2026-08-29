public struct CreateTopicBody: Encodable, Sendable {
    public let name: String
    public let slug: String
    public let topicType: String?
    public let markdown: String?
    public let hostname: String?
    public let sourceTopicAliasId: String?

    public init(
        name: String,
        slug: String,
        topicType: String? = nil,
        markdown: String? = nil,
        hostname: String? = nil,
        sourceTopicAliasId: String? = nil
    ) {
        self.name = name
        self.slug = slug
        self.topicType = topicType
        self.markdown = markdown
        self.hostname = hostname
        self.sourceTopicAliasId = sourceTopicAliasId
    }
}
