public struct PostImagePlacement: Codable, Identifiable, Sendable {
    public let imageId: String
    public let placementId: String
    public let placementRevision: Int
    public let orderIndex: Int
    public let caption: String

    public var id: String {
        placementId
    }
}

public struct PostImagePlacementResponse: Codable, Sendable {
    public let images: [PostImagePlacement]
}

public enum CopyrightImageSimilarityAvailability: String, Codable, Sendable {
    case available, unavailable
}

public struct CopyrightImageSimilarityCandidate: Codable, Identifiable, Sendable {
    public let placementId: String
    public let placementRevision: Int
    public let imageId: String
    public let postId: String
    public let similarity: Double

    public var id: String {
        placementId
    }
}

public struct CopyrightSimilarityCandidatesResponse: Codable, Sendable {
    public let availability: CopyrightImageSimilarityAvailability
    public let copyrightImageSimilarityCandidates: [CopyrightImageSimilarityCandidate]
}
