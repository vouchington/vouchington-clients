import Foundation

public struct CommunityBanLiftFailure: Error, Sendable {
    public let statusCode: Int
    public let code: String
    public let cap: Int?
}
