import Foundation
import VouchaModels
import VouchaTestSupport
import XCTest

final class NativeMembershipOverviewTests: XCTestCase {
    func testDefaultOverviewRoundTripsAndStaysFree() throws {
        let overview = try decodeOverview("native.memberships.me.default")
        XCTAssertNil(overview.membership)
        XCTAssertEqual(overview.visiblePlan, "free")
        XCTAssertFalse(overview.blocksStoreReplacement)
        XCTAssertEqual(overview.pending.grants, 0)
        try assertRoundTrip("native.memberships.me.default", as: NativeMembershipOverview.self)
    }

    func testLifecycleOverviewKeepsIneffectiveSourcesAndPendingWork() throws {
        let overview = try decodeOverview("native.memberships.me.lifecycle.default")
        XCTAssertNil(overview.membership)
        XCTAssertEqual(overview.visiblePlan, "free")
        XCTAssertTrue(overview.blocksStoreReplacement)
        XCTAssertEqual(overview.pending.grants, 1)
        XCTAssertEqual(overview.pending.financialOperations.map(\.kind), ["automatic_refund"])
        XCTAssertEqual(overview.pending.switches.first?.eligibleAt, "2027-01-01T00:00:00.000Z")
        XCTAssertEqual(
            overview.pending.verifications.map(\.reasonCode),
            ["competing_direct_source", "wrong_account"]
        )
        XCTAssertEqual(overview.sources.map(\.isEffective), [false, false])
        XCTAssertNil(overview.sources[1].renewal?.price)
        XCTAssertEqual(overview.management?.destination, "apple_subscriptions")
        try assertRoundTrip("native.memberships.me.lifecycle.default", as: NativeMembershipOverview.self)
    }

    func testVerificationStatusFixturesRoundTrip() throws {
        let names = [
            "native.memberships.verification-status.pending.default",
            "native.memberships.verification-status.verified.default",
            "native.memberships.verification-status.conflict.default",
            "native.memberships.verification-status.rejected.default",
            "native.memberships.verification.pending.default"
        ]
        for name in names {
            let response = try makeVouchaDecoder().decode(
                NativeMembershipVerificationResponse.self,
                from: nativeMembershipFixture(name)
            )
            XCTAssertFalse(response.verification.id.isEmpty, name)
            try assertRoundTrip(name, as: NativeMembershipVerificationResponse.self)
        }
        XCTAssertFalse(
            MembershipVerificationPolling.isTerminal(status: "pending", reasonCode: "purchase_pending")
        )
        XCTAssertTrue(MembershipVerificationPolling.isTerminal(status: "rejected", reasonCode: "wrong_account"))
        for name in [
            "native.memberships.purchase-intent.apple.default",
            "native.memberships.purchase-intent.google.default",
            "native.memberships.purchase-intent.microsoft.default",
            "native.memberships.purchase-intent.stripe.default"
        ] {
            try assertRoundTrip(name, as: NativeMembershipPurchaseIntentResponse.self)
        }
        try assertRoundTrip(
            "native.memberships.purchase-intent.conflict.default",
            as: NativeMembershipPurchaseConflict.self
        )
    }

    func testCatalogFailsClosedForUnpairedAndUnpricedStoreRows() throws {
        let plans = try makeVouchaDecoder().decode(
            MembershipPlansResponse.self,
            from: nativeMembershipFixture("native.memberships.plans.default")
        )
        let products = plans.products
        let empty = try decodeOverview("native.memberships.me.default")
        XCTAssertEqual(
            NativeMembershipCatalog.rows(in: products, for: .appleIOS).map(\.storeProductId),
            ["voucha.plus.monthly"]
        )
        XCTAssertEqual(
            NativeMembershipCatalog.rows(in: products, for: .microsoftStore).map(\.applicationId),
            ["Voucha"]
        )
        XCTAssertTrue(NativeMembershipCatalog.rows(in: products, for: .appleMacOS).isEmpty)
        XCTAssertTrue(NativeMembershipCatalog.rows(in: products, for: .macCatalyst).isEmpty)

        let apple = try purchase("native.memberships.purchase-intent.apple.default")
        XCTAssertNil(refusal(apple, surface: .appleIOS, products: products, overview: empty))
        XCTAssertEqual(
            refusal(apple, surface: .appleMacOS, products: products, overview: empty),
            .noStoreRow
        )

        let google = try purchase("native.memberships.purchase-intent.google.default")
        XCTAssertEqual(
            refusal(google, surface: .googlePlay, products: products, overview: empty),
            .noStoreRow
        )
        XCTAssertEqual(
            NativeMembershipCatalog.refusal(
                for: NativeMembershipPurchaseIntent(
                    id: google.purchaseIntent.id,
                    launch: google.purchaseIntent.launch,
                    productId: "00000000-0000-7000-8000-000000000702",
                    provider: google.purchaseIntent.provider,
                    replayed: google.purchaseIntent.replayed
                ),
                products: products,
                store: .googlePlay,
                overview: empty
            ),
            .playLaunchMismatch
        )

        let microsoft = try purchase("native.memberships.purchase-intent.microsoft.default")
        XCTAssertEqual(
            refusal(microsoft, surface: .microsoftStore, products: products, overview: empty),
            .noStoreRow
        )
        let stripe = try purchase("native.memberships.purchase-intent.stripe.default")
        XCTAssertEqual(
            refusal(stripe, surface: .appleIOS, products: products, overview: empty),
            .stripeCheckout
        )
        let lifecycle = try decodeOverview("native.memberships.me.lifecycle.default")
        XCTAssertEqual(
            refusal(apple, surface: .appleIOS, products: products, overview: lifecycle),
            .replacementUnavailable
        )
    }

    private func refusal(
        _ response: NativeMembershipPurchaseIntentResponse,
        surface: NativeMembershipStore,
        products: [MembershipCatalogProduct],
        overview: NativeMembershipOverview
    ) -> MembershipPurchaseRefusal? {
        NativeMembershipCatalog.refusal(
            for: response.purchaseIntent,
            products: products,
            store: surface,
            overview: overview
        )
    }
}

func nativeMembershipFixture(_ name: String) throws -> Data {
    let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"])
    let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/responses/\(name).json", root: root)
    return try Data(contentsOf: url)
}

func decodeOverview(_ name: String) throws -> NativeMembershipOverview {
    try makeVouchaDecoder().decode(NativeMembershipOverview.self, from: nativeMembershipFixture(name))
}

func purchase(_ name: String) throws -> NativeMembershipPurchaseIntentResponse {
    try makeVouchaDecoder().decode(NativeMembershipPurchaseIntentResponse.self, from: nativeMembershipFixture(name))
}

func assertRoundTrip<T: Codable>(
    _ name: String,
    as _: T.Type,
    file: StaticString = #filePath,
    line: UInt = #line
) throws {
    let original = try nativeMembershipFixture(name)
    let decoded = try makeVouchaDecoder().decode(T.self, from: original)
    let encoder = JSONEncoder()
    encoder.keyEncodingStrategy = .convertToSnakeCase
    let missing = try JsonKeyPaths.extract(original).subtracting(JsonKeyPaths.extract(encoder.encode(decoded)))
    XCTAssertTrue(missing.isEmpty, "\(name) dropped \(missing.sorted())", file: file, line: line)
}
