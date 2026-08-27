import VouchaAPI
import VouchaModels

@MainActor
protocol PaymentCardServicing {
    func cards(after: String?) async throws -> PaymentCardPage
    func searchCardTopics(query: String) async throws -> [TopicSearchResult]
    func create(cardId: String) async throws -> PaymentCard
    func update(id: String, body: UpdatePaymentCardBody) async throws -> PaymentCard
    func delete(id: String) async throws
}

@MainActor
final class PaymentCardService: PaymentCardServicing {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func cards(after: String?) async throws -> PaymentCardPage {
        try await client.send(.paymentCards(after: after))
    }

    func searchCardTopics(query: String) async throws -> [TopicSearchResult] {
        let response: TopicSearchResponse = try await client.send(.paymentCardTopics(query: query))
        return response.results.filter { $0.topicType == "card" }
    }

    func create(cardId: String) async throws -> PaymentCard {
        let response: PaymentCardResponse = try await client.send(.createPaymentCard(body: .init(cardId: cardId)))
        return response.card
    }

    func update(id: String, body: UpdatePaymentCardBody) async throws -> PaymentCard {
        let response: PaymentCardResponse = try await client.send(.updatePaymentCard(id: id, body: body))
        return response.card
    }

    func delete(id: String) async throws {
        _ = try await client.data(for: .deletePaymentCard(id: id))
    }
}
