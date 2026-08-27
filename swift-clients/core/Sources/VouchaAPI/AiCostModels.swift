import VouchaModels

public struct CommunityAiCostTotal: Codable, Identifiable, Sendable, Hashable {
    public let communityId: String
    public let communitySlug: String
    public let requestCount: Int64
    public let totalInputTokens: Int64
    public let totalOutputTokens: Int64
    public let unpricedRequestCount: Int64
    public let totalCost: ScaledMoneyAggregate

    public var id: String {
        communityId
    }
}

public typealias AiCostTotalsPage = Page<CommunityAiCostTotal>
