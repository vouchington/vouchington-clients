public extension Endpoint {
    static func linkTopicAlias(topicId: String, aliasId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/topics/\(pathSegment(topicId))/aliases/\(pathSegment(aliasId))",
            body: [String: String]()
        )
    }
}
