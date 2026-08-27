import VouchaAPI
import VouchaModels

@MainActor
protocol PointValuationServicing {
    func valuations(after: String?) async throws -> PointValuationPage
    func searchRewardsPrograms(query: String) async throws -> [TopicSearchResult]
    func create(body: CreatePointValuationBody) async throws -> PointValuation
    func update(id: String, body: UpdatePointValuationBody) async throws -> PointValuation
    func delete(id: String) async throws
}

@MainActor
final class PointValuationService: PointValuationServicing {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func valuations(after: String?) async throws -> PointValuationPage {
        try await client.send(.pointValuations(after: after))
    }

    func searchRewardsPrograms(query: String) async throws -> [TopicSearchResult] {
        let response: TopicSearchResponse = try await client.send(.rewardsProgramTopics(query: query))
        return response.results.filter { $0.topicType == "rewards_program" && $0.name != nil }
    }

    func create(body: CreatePointValuationBody) async throws -> PointValuation {
        let response: PointValuationResponse = try await client.send(.createPointValuation(body: body))
        return response.pointValuation
    }

    func update(id: String, body: UpdatePointValuationBody) async throws -> PointValuation {
        let response: PointValuationResponse = try await client.send(.updatePointValuation(id: id, body: body))
        return response.pointValuation
    }

    func delete(id: String) async throws {
        _ = try await client.data(for: .deletePointValuation(id: id))
    }
}
