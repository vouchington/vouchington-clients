import Foundation

public struct PaymentCardTopic: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String

    public init(id: String, name: String, slug: String) {
        self.id = id
        self.name = name
        self.slug = slug
    }
}

public struct PaymentCardParentSummary: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let openedOn: LocalDate?
    public let closedOn: LocalDate?
    public let card: PaymentCardTopic

    public init(id: String, openedOn: LocalDate?, closedOn: LocalDate?, card: PaymentCardTopic) {
        self.id = id
        self.openedOn = openedOn
        self.closedOn = closedOn
        self.card = card
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(openedOn, forKey: .openedOn)
        try container.encode(closedOn, forKey: .closedOn)
        try container.encode(card, forKey: .card)
    }

    private enum CodingKeys: String, CodingKey {
        case id, openedOn, closedOn, card
    }
}

public struct PaymentCard: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let cardId: String
    public let openedOn: LocalDate?
    public let closedOn: LocalDate?
    public let receivedSignUpBonusOn: LocalDate?
    public let creditLimit: Money?
    public let isAuthorizedUser: Bool
    public let authorizedUserOfId: String?
    public let note: String?
    public let card: PaymentCardTopic
    public let authorizedUserOfCard: PaymentCardParentSummary?

    public init(
        id: String,
        cardId: String,
        openedOn: LocalDate?,
        closedOn: LocalDate?,
        receivedSignUpBonusOn: LocalDate?,
        creditLimit: Money?,
        isAuthorizedUser: Bool,
        authorizedUserOfId: String?,
        note: String?,
        card: PaymentCardTopic,
        authorizedUserOfCard: PaymentCardParentSummary?
    ) {
        self.id = id
        self.cardId = cardId
        self.openedOn = openedOn
        self.closedOn = closedOn
        self.receivedSignUpBonusOn = receivedSignUpBonusOn
        self.creditLimit = creditLimit
        self.isAuthorizedUser = isAuthorizedUser
        self.authorizedUserOfId = authorizedUserOfId
        self.note = note
        self.card = card
        self.authorizedUserOfCard = authorizedUserOfCard
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        cardId = try container.decode(String.self, forKey: .cardId)
        openedOn = try container.decodeIfPresent(LocalDate.self, forKey: .openedOn)
        closedOn = try container.decodeIfPresent(LocalDate.self, forKey: .closedOn)
        receivedSignUpBonusOn = try container.decodeIfPresent(LocalDate.self, forKey: .receivedSignUpBonusOn)
        creditLimit = try container.decodeIfPresent(Money.self, forKey: .creditLimit)
        isAuthorizedUser = try container.decode(Bool.self, forKey: .isAuthorizedUser)
        authorizedUserOfId = try container.decodeIfPresent(String.self, forKey: .authorizedUserOfId)
        note = try container.decodeIfPresent(String.self, forKey: .note)
        card = try container.decode(PaymentCardTopic.self, forKey: .card)
        authorizedUserOfCard = try container.decodeIfPresent(
            PaymentCardParentSummary.self,
            forKey: .authorizedUserOfCard
        )
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(cardId, forKey: .cardId)
        try container.encode(openedOn, forKey: .openedOn)
        try container.encode(closedOn, forKey: .closedOn)
        try container.encode(receivedSignUpBonusOn, forKey: .receivedSignUpBonusOn)
        try container.encode(creditLimit, forKey: .creditLimit)
        try container.encode(isAuthorizedUser, forKey: .isAuthorizedUser)
        try container.encode(authorizedUserOfId, forKey: .authorizedUserOfId)
        try container.encode(note, forKey: .note)
        try container.encode(card, forKey: .card)
        try container.encode(authorizedUserOfCard, forKey: .authorizedUserOfCard)
    }

    private enum CodingKeys: String, CodingKey {
        case id, openedOn, closedOn, receivedSignUpBonusOn, creditLimit
        case cardId = "cardTopicId"
        case isAuthorizedUser, note, card, authorizedUserOfCard
        case authorizedUserOfId = "authorizedUserOfCardId"
    }
}

public typealias PaymentCardPage = Page<PaymentCard>

public struct PaymentCardResponse: Codable, Sendable {
    public let card: PaymentCard
}
