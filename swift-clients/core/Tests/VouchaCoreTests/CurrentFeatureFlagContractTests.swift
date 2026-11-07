import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CurrentFeatureFlagContractTests: XCTestCase {
    func testLandedPublicFeatureFlagsDecodeWithoutRetiredKeys() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            FeatureFlagsResponse.self, from: ApiFixtureLoader.data("native.feature-flags.default")
        )
        XCTAssertEqual(response.flags, ["fediverse": false, "memberships": false])
        XCTAssertEqual(response.overrides, [:])
        XCTAssertTrue(Set(response.flags.keys).isDisjoint(with: retiredFeatureFlags))
    }

    func testChangedAndNoOpDynamicConfigDecodeCurrentFieldsWithoutRetiredKeys() throws {
        for (fixture, changed, fediverse) in [
            ("native.dynamic-config.update.changed", true, true),
            ("native.dynamic-config.update.no-op", false, false)
        ] {
            let response = try JSONDecoder.vouchaFixtureDecoder.decode(
                DynamicConfigUpdateResponse.self, from: ApiFixtureLoader.data(fixture)
            )
            XCTAssertEqual(response.changed, changed)
            XCTAssertEqual(response.namespace.namespace, "feature-flags")
            XCTAssertEqual(
                response.namespace.config,
                ["fediverse": .boolean(fediverse), "memberships": .boolean(false)]
            )
            XCTAssertEqual(Set(response.namespace.fields.map(\.name)), ["fediverse", "memberships"])
            XCTAssertEqual(response.namespace.fieldCount, 2)
            XCTAssertTrue(Set(response.namespace.config.keys)
                .isDisjoint(with: retiredFeatureFlags.union(retiredBillingFields)))
            XCTAssertTrue(Set(response.namespace.fields.map(\.name))
                .isDisjoint(with: retiredFeatureFlags.union(retiredBillingFields)))
        }
    }

    private let retiredFeatureFlags: Set<String> = [
        "membershipAppleBilling", "membershipGoogleBilling", "membershipMicrosoftBilling",
        "chat", "support", "combinedSearch"
    ]
    private let retiredBillingFields: Set<String> = [
        "stripe_automatic_collision_resolution_enabled", "apple_app_store_automatic_collision_resolution_enabled",
        "google_play_automatic_collision_resolution_enabled", "microsoft_store_automatic_collision_resolution_enabled"
    ]
}
