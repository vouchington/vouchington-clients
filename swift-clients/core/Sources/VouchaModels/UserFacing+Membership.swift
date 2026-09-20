import Foundation

public struct MembershipSkuSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let plan: String
    public let price: Money
    public let interval: String
    public let stripePriceId: String
}

public struct MembershipProductProvider: Codable, Sendable {
    public let applicationId: String
    @RequiredNullable
    public var basePlanId: String?
    public let environment: String
    @RequiredNullable
    public var offerId: String?
    @RequiredNullable
    public var price: Money?
    public let productId: String
    public let provider: String
    @RequiredNullable
    public var skuId: String?
}

public struct MembershipProduct: Codable, Identifiable, Sendable {
    public let id: String
    public let interval: String
    public let plan: String
    public let providers: [MembershipProductProvider]
}

public struct MembershipPlansResponse: Codable, Sendable {
    public let products: [MembershipProduct]
    public let benefitCatalog: MembershipBenefitCatalog?

    public var plans: [String: [MembershipSkuSummary]] {
        Dictionary(grouping: products.map(\.skuSummary), by: \.plan)
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        benefitCatalog = try container.decodeIfPresent(MembershipBenefitCatalog.self, forKey: .benefitCatalog)
        if let products = try container.decodeIfPresent([MembershipProduct].self, forKey: .products) {
            self.products = products
        } else {
            let legacyPlans = try container.decodeIfPresent(
                [String: [MembershipSkuSummary]].self,
                forKey: .plans
            ) ?? [:]
            products = legacyPlans.values.flatMap { $0 }.map(MembershipProduct.init(sku:))
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(products, forKey: .products)
        try container.encodeIfPresent(benefitCatalog, forKey: .benefitCatalog)
    }

    private enum CodingKeys: String, CodingKey {
        case products, plans, benefitCatalog
    }
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

private extension MembershipProduct {
    init(sku: MembershipSkuSummary) {
        id = sku.id
        interval = sku.interval
        plan = sku.plan
        providers = [
            MembershipProductProvider(
                applicationId: "voucha-web",
                basePlanId: nil,
                environment: "test",
                offerId: nil,
                price: sku.price,
                productId: sku.stripePriceId,
                provider: "stripe",
                skuId: nil
            )
        ]
    }

    var skuSummary: MembershipSkuSummary {
        let stripe = providers.first { $0.provider == "stripe" }
        let price = stripe?.price
            ?? providers.compactMap(\.price).first
            ?? MembershipCatalogPricing.fallbackPrice
        return MembershipSkuSummary(
            id: id,
            plan: plan,
            price: price,
            interval: interval,
            stripePriceId: stripe?.productId ?? ""
        )
    }
}

private enum MembershipCatalogPricing {
    static let fallbackPrice: Money = {
        do {
            return try Money(amount: 0, currency: "usd")
        } catch {
            preconditionFailure("usd/0 is a valid Money")
        }
    }()
}
