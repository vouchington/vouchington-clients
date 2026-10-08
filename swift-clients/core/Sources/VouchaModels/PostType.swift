public enum PostType: String, Codable, Hashable, Sendable {
    case discussion, review, dataPoint = "data_point", comment
    case article, blogPost = "blog_post", story, link
    case topicRecommendation = "topic_recommendation"
}
