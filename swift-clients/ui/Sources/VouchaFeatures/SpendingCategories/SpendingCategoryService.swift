import VouchaAPI
import VouchaModels

@MainActor
protocol SpendingCategoryServicing {
    func categories(after: String?) async throws -> SpendingCategoryPage
    func searchTopics(query: String) async throws -> [TopicSearchResult]
    func create(body: CreateSpendingCategoryBody) async throws -> SpendingCategory
    func update(id: String, body: UpdateSpendingCategoryBody) async throws -> SpendingCategory
    func delete(id: String) async throws
}

@MainActor
final class SpendingCategoryService: SpendingCategoryServicing {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func categories(after: String?) async throws -> SpendingCategoryPage {
        try await client.send(.spendingCategories(after: after))
    }

    func searchTopics(query: String) async throws -> [TopicSearchResult] {
        let response: TopicSearchResponse = try await client.send(.spendingCategoryTopics(query: query))
        return response.results.filter { $0.name != nil }
    }

    func create(body: CreateSpendingCategoryBody) async throws -> SpendingCategory {
        let response: SpendingCategoryResponse = try await client.send(.createSpendingCategory(body: body))
        return response.spendingCategory
    }

    func update(id: String, body: UpdateSpendingCategoryBody) async throws -> SpendingCategory {
        let response: SpendingCategoryResponse = try await client.send(.updateSpendingCategory(id: id, body: body))
        return response.spendingCategory
    }

    func delete(id: String) async throws {
        _ = try await client.data(for: .deleteSpendingCategory(id: id))
    }
}
