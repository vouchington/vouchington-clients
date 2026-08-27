import VouchaAPI
import VouchaModels

@MainActor
protocol RewardsProgramStatusServicing {
    func statuses(after: String?) async throws -> RewardsProgramStatusPage
    func searchStatuses(query: String) async throws -> [TopicSearchResult]
    func create(body: CreateRewardsProgramStatusBody) async throws -> RewardsProgramStatus
    func update(id: String, body: UpdateRewardsProgramStatusBody) async throws -> RewardsProgramStatus
    func delete(id: String) async throws
}

@MainActor
final class RewardsProgramStatusService: RewardsProgramStatusServicing {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func statuses(after: String?) async throws -> RewardsProgramStatusPage {
        try await client.send(.rewardsProgramStatuses(after: after))
    }

    func searchStatuses(query: String) async throws -> [TopicSearchResult] {
        let response: TopicSearchResponse = try await client.send(.rewardsProgramStatusTopics(query: query))
        return response.results.filter { $0.topicType == "rewards_program_status" && $0.name != nil }
    }

    func create(body: CreateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        let response: RewardsProgramStatusResponse = try await client.send(.createRewardsProgramStatus(body: body))
        return response.rewardsProgramStatus
    }

    func update(id: String, body: UpdateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        let response: RewardsProgramStatusResponse = try await client.send(.updateRewardsProgramStatus(
            id: id,
            body: body
        ))
        return response.rewardsProgramStatus
    }

    func delete(id: String) async throws {
        _ = try await client.data(for: .deleteRewardsProgramStatus(id: id))
    }
}
