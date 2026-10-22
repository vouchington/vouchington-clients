import Foundation

public enum NativeMembershipStore: String, Sendable, Equatable {
    case appleIOS
    case appleMacOS
    case googlePlay
    case microsoftStore
    case macCatalyst
}

public enum MembershipPurchaseRefusal: String, Sendable, Equatable {
    case conflict
    case stripeCheckout
    case noStoreRow
    case playLaunchMismatch
    case replacementUnavailable
}

public struct NativeMembershipStoreRow: Equatable, Sendable {
    public let canonicalProductId: String
    public let plan: String
    public let provider: String
    public let applicationId: String
    public let storeProductId: String
    public let basePlanId: String?
    public let skuId: String?
}

public enum NativeMembershipCatalog {
    public static let appleIOSApplication = "ai.voucha.ios"
    public static let appleMacOSApplication = "ai.voucha.macos"
    public static let androidApplication = "ai.voucha.android"

    public static func rows(
        in products: [MembershipCatalogProduct],
        for store: NativeMembershipStore
    ) -> [NativeMembershipStoreRow] {
        products.flatMap { product in
            product.providers.compactMap { provider in
                guard matches(provider, store: store) else { return nil }
                return NativeMembershipStoreRow(
                    canonicalProductId: product.id,
                    plan: product.plan,
                    provider: provider.provider,
                    applicationId: provider.applicationId,
                    storeProductId: provider.productId,
                    basePlanId: provider.basePlanId,
                    skuId: provider.skuId
                )
            }
        }
    }

    public static func refusal(
        for intent: NativeMembershipPurchaseIntent,
        products: [MembershipCatalogProduct],
        store: NativeMembershipStore,
        overview: NativeMembershipOverview
    ) -> MembershipPurchaseRefusal? {
        let provider = intent.provider
        let launch = intent.launch
        if provider == "stripe" || membershipLaunchString(launch, "kind") == "stripe_checkout" {
            return .stripeCheckout
        }
        if !surfaceAccepts(provider, store: store) {
            return .noStoreRow
        }
        if overview.blocksStoreReplacement {
            return .replacementUnavailable
        }
        let matched = rows(in: products, for: store).filter { $0.canonicalProductId == intent.productId }
        guard matched.count == 1, let row = matched.first else {
            return .noStoreRow
        }
        if store == .googlePlay, !playLaunchMatches(row, launch) {
            return .playLaunchMismatch
        }
        if store == .appleIOS, membershipLaunchString(launch, "product_id") != row.storeProductId {
            return .noStoreRow
        }
        return nil
    }

    private static func matches(
        _ provider: MembershipCatalogProvider,
        store: NativeMembershipStore
    ) -> Bool {
        switch store {
        case .appleIOS:
            provider.provider == "apple_app_store" && provider.applicationId == appleIOSApplication
        case .appleMacOS:
            provider.provider == "apple_app_store" && provider.applicationId == appleMacOSApplication
        case .googlePlay:
            provider.provider == "google_play" && provider.applicationId == androidApplication
        case .microsoftStore:
            provider.provider == "microsoft_store"
        case .macCatalyst:
            false
        }
    }

    private static func surfaceAccepts(_ provider: String, store: NativeMembershipStore) -> Bool {
        switch store {
        case .appleIOS, .appleMacOS:
            provider == "apple_app_store"
        case .googlePlay:
            provider == "google_play"
        case .microsoftStore:
            provider == "microsoft_store"
        case .macCatalyst:
            false
        }
    }

    private static func playLaunchMatches(
        _ row: NativeMembershipStoreRow,
        _ launch: DecodedJSONValue
    ) -> Bool {
        membershipLaunchString(launch, "product_id") == row.storeProductId &&
            membershipLaunchString(launch, "base_plan_id") == row.basePlanId
    }
}

private func membershipLaunchString(_ launch: DecodedJSONValue, _ key: String) -> String? {
    guard case let .object(object) = launch, case let .string(value) = object[key] else {
        return nil
    }
    return value
}
