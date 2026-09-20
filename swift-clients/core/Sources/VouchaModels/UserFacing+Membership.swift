import Foundation

public struct MembershipSkuSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let plan: String
    public let price: Money
    public let interval: String
    public let stripePriceId: String
}

public struct MembershipPlansResponse: Codable, Sendable {
    public let products: [MembershipCatalogProduct]
    public let benefitCatalog: MembershipBenefitCatalog?

    /// The administrator grant surface supports direct Stripe products only.
    /// Keep its existing plan/SKU presentation derived from the provider catalog rather than
    /// accepting the retired `plans` response shape.
    public var plans: [String: [MembershipSkuSummary]] {
        products.reduce(into: [:]) { result, product in
            guard let stripe = product.providers.first(where: { $0.provider == "stripe" }),
                  let price = stripe.price
            else {
                return
            }
            result[product.plan, default: []].append(
                MembershipSkuSummary(
                    id: product.id,
                    plan: product.plan,
                    price: price,
                    interval: product.interval,
                    stripePriceId: stripe.productId
                )
            )
        }
    }
}

public struct MembershipCatalogProduct: Codable, Identifiable, Sendable {
    public let id: String
    public let plan: String
    public let interval: String
    public let providers: [MembershipCatalogProvider]
}

public struct MembershipCatalogProvider: Codable, Sendable {
    public let provider: String
    public let environment: String
    public let applicationId: String
    public let productId: String
    @RequiredNullable
    public var basePlanId: String?
    @RequiredNullable
    public var offerId: String?
    @RequiredNullable
    public var skuId: String?
    public let price: Money?
}

public struct MembershipBenefitCatalog: Codable, Sendable {
    public let version: Int
    public let groups: [MembershipBenefitGroup]
}

public struct MembershipBenefitGroup: Codable, Sendable {
    public let id: String
    public let benefits: [MembershipBenefit]
}

public struct MembershipBenefit: Codable, Sendable {
    public let id: String
    public let placements: [String]
    public let values: [String: MembershipBenefitValue]
}

public struct MembershipBenefitValue: Codable, Sendable {
    public let kind: String
    public let access: String?
    public let included: Bool?
    public let level: String?
    public let quantity: Int?
}
