import Foundation
import VouchaModels

public struct CreatePaymentCardBody: Encodable, Sendable {
    public let cardId: String

    public init(cardId: String) {
        self.cardId = cardId
    }
}

public struct UpdatePaymentCardBody: Encodable, Sendable {
    public let openedOn: NullableValue<LocalDate>?
    public let closedOn: NullableValue<LocalDate>?
    public let receivedSignUpBonusOn: NullableValue<LocalDate>?
    public let creditLimit: NullableValue<Money>?
    public let isAuthorizedUser: Bool?
    public let authorizedUserOfId: NullableValue<String>?
    public let note: NullableValue<String>?

    public init(
        openedOn: NullableValue<LocalDate>? = nil,
        closedOn: NullableValue<LocalDate>? = nil,
        receivedSignUpBonusOn: NullableValue<LocalDate>? = nil,
        creditLimit: NullableValue<Money>? = nil,
        isAuthorizedUser: Bool? = nil,
        authorizedUserOfId: NullableValue<String>? = nil,
        note: NullableValue<String>? = nil
    ) {
        self.openedOn = openedOn
        self.closedOn = closedOn
        self.receivedSignUpBonusOn = receivedSignUpBonusOn
        self.creditLimit = creditLimit
        self.isAuthorizedUser = isAuthorizedUser
        self.authorizedUserOfId = authorizedUserOfId
        self.note = note
    }
}

public extension Endpoint {
    static func paymentCards(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: String(limit))]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/cards", queryItems: queryItems)
    }

    static func createPaymentCard(body: CreatePaymentCardBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/cards", body: body)
    }

    static func updatePaymentCard(id: String, body: UpdatePaymentCardBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/cards/\(pathSegment(id))", body: body)
    }

    static func deletePaymentCard(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/cards/\(pathSegment(id))")
    }

    static func paymentCardTopics(query: String, limit: Int = 10) -> Endpoint {
        topics(query: query, topicTypes: ["card"], limit: limit)
    }
}
