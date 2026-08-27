public struct PodcastChapter: Codable, Sendable {
    public let startSeconds: Double
    public let endSeconds: Double?
    public let title: String
    public let url: String?
    public let imageUrl: String?
    public let isVisible: Bool

    public var imageURL: String? {
        imageUrl
    }

    public init(
        startSeconds: Double,
        endSeconds: Double?,
        title: String,
        url: String?,
        imageURL: String?,
        isVisible: Bool
    ) {
        self.startSeconds = startSeconds
        self.endSeconds = endSeconds
        self.title = title
        self.url = url
        imageUrl = imageURL
        self.isVisible = isVisible
    }
}

public struct PodcastChapterResponse: Codable, Sendable {
    public let chapters: [PodcastChapter]

    public init(chapters: [PodcastChapter]) {
        self.chapters = chapters
    }
}
