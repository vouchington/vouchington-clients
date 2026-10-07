import Foundation

public struct FediverseInstanceAttributes: Codable, Sendable {
    @RequiredNullable public var software: String?
    @RequiredNullable public var nodeinfoSoftwareVersion: String?
    @RequiredNullable public var protocolName: String?
    @RequiredNullable public var totalUsers: Int?
    @RequiredNullable public var monthlyActiveUsers: Int?
    @RequiredNullable public var openRegistrations: Bool?

    private enum CodingKeys: String, CodingKey {
        case software, nodeinfoSoftwareVersion
        case protocolName = "protocol"
        case totalUsers, monthlyActiveUsers
        case openRegistrations = "isOpenForRegistrations"
    }
}

public struct HostnameElection: Codable, Sendable {
    public let id: String
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int

    public init(id: String, votesScoreNet: Double, votesCountUp: Int, votesCountDown: Int) {
        self.id = id
        self.votesScoreNet = votesScoreNet
        self.votesCountUp = votesCountUp
        self.votesCountDown = votesCountDown
    }
}

public struct FediverseInstanceListItem: Identifiable, Sendable {
    public let topic: Topic
    public let instance: FediverseInstanceAttributes
    public let topicElection: TopicElection?
    public let hostnameElection: HostnameElection?

    public var id: String {
        topic.id
    }
}

public struct FediverseInstancesResponse: Codable, Sendable {
    public let results: [TopicSearchResult]
    public let pageInfo: Page<TopicSearchResult>.PageInfo
    public let topics: [String: Topic]
    public let fediverseInstances: [String: FediverseInstanceAttributes]
    public let topicElections: [String: TopicElection]
    public let hostnameElections: [String: HostnameElection]

    public var orderedInstances: [FediverseInstanceListItem] {
        results.compactMap { result in
            guard let topic = topics[result.id], let instance = fediverseInstances[result.id] else { return nil }
            return FediverseInstanceListItem(
                topic: topic,
                instance: instance,
                topicElection: topicElections[result.id],
                hostnameElection: topic.hostnameId.flatMap { hostnameElections[$0] }
            )
        }
    }
}

public struct FediverseInstanceDetailResponse: Codable, Sendable {
    public let topic: Topic
    @RequiredNullable public var instance: FediverseInstanceAttributes?
    @RequiredNullable public var topicElection: TopicElection?
    @RequiredNullable public var hostnameElection: HostnameElection?

    private enum CodingKeys: String, CodingKey {
        case topic
        case instance = "fediverseInstance"
        case topicElection, hostnameElection
    }
}
