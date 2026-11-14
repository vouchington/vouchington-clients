import Foundation

public struct NativeMembershipPurchaseIntentResponse: Codable, Sendable {
    public let purchaseIntent: NativeMembershipPurchaseIntent
}

public struct NativeMembershipPurchaseIntent: Codable, Sendable {
    public let id: String
    public let launch: DecodedJSONValue
    public let productId: String
    public let provider: String
    public let replayed: Bool

    public init(
        id: String,
        launch: DecodedJSONValue,
        productId: String,
        provider: String,
        replayed: Bool
    ) {
        self.id = id
        self.launch = launch
        self.productId = productId
        self.provider = provider
        self.replayed = replayed
    }
}

public struct NativeMembershipPurchaseConflict: Codable, Sendable {
    public let code: String
    public let eligibleAt: String
    public let error: String
    public let management: NativeMembershipManagement
}

public struct NativeMembershipVerificationResponse: Codable, Sendable {
    public let verification: NativeMembershipVerification
}

public struct NativeMembershipVerification: Codable, Sendable {
    public let createdAt: String
    public let id: String
    public let provider: String
    @RequiredNullable public var reasonCode: String?
    public let status: String
}

public struct NativeMembershipIntentBody: Encodable, Equatable, Sendable {
    public let idempotencyKey: String
    public let productId: String
    public let provider: String

    public init(idempotencyKey: String, productId: String, provider: String) {
        self.idempotencyKey = idempotencyKey
        self.productId = productId
        self.provider = provider
    }
}

public struct NativeMembershipVerificationSubmission: Encodable, Sendable {
    public let evidence: DecodedJSONValue
    public let idempotencyKey: String
    public let provider: String
    public let purchaseIntentId: String

    public init(
        evidence: DecodedJSONValue,
        idempotencyKey: String,
        provider: String,
        purchaseIntentId: String
    ) {
        self.evidence = evidence
        self.idempotencyKey = idempotencyKey
        self.provider = provider
        self.purchaseIntentId = purchaseIntentId
    }
}
