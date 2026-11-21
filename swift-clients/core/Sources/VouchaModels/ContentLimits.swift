/// Server-configured content caps from `GET /api/v1/content-limits`.
public struct ContentLimitsResponse: Codable, Sendable, Equatable {
    public let postImagesMax: Int
    public let copyrightClaimantTargetsMax: Int
    public let followerDistributionRecipientsMax: Int
}
