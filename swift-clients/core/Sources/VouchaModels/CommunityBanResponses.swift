import Foundation

public struct CommunityBanResponse: Codable, Sendable {
    public let communityBan: CommunityBan
}

public struct CommunityBanLiftResponse: Codable, Sendable {
    public let communityId: String
    public let userId: String
    public let liftedBanIds: [String]
}
