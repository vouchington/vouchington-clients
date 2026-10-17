public struct EngineeringDomainStateCount: Codable, Sendable, Hashable {
    public let count: Int
    public let complete: Bool
}

public struct EngineeringQueueDomain: Codable, Sendable, Hashable {
    public let domain: String
    public let paused: Bool
    public let pausedAt: String?
    public let pausedBy: String?
    public let reason: String?
    public let waiting: EngineeringDomainStateCount
    public let active: EngineeringDomainStateCount
    public let delayed: EngineeringDomainStateCount
    public let completed: EngineeringDomainStateCount
    public let failed: EngineeringDomainStateCount
}

public struct EngineeringQueueDomainsResponse: Codable, Sendable {
    public let domains: [EngineeringQueueDomain]
}

public struct EngineeringRetryDomainResponse: Codable, Sendable {
    public let success: Bool
    public let retried: Int
}
