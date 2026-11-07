import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CurrentFeatureFlagContractTests: XCTestCase {
    func testLandedPublicFeatureFlagsDecodeWithoutRetiredKeys() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            FeatureFlagsResponse.self, from: ApiFixtureLoader.data("native.feature-flags.default")
        )
        XCTAssertEqual(response.flags["fediverse"], false)
        XCTAssertEqual(response.flags["memberships"], false)
        let raw = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.feature-flags.default")
        ) as? [String: Any])
        XCTAssertEqual(Set(response.flags.keys), try Set(XCTUnwrap(raw["flags"] as? [String: Any]).keys))
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
            XCTAssertEqual(response.namespace.config["fediverse"], .boolean(fediverse))
            XCTAssertEqual(response.namespace.config["memberships"], .boolean(false))
            let raw = try XCTUnwrap(JSONSerialization
                .jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any])
            let namespace = try XCTUnwrap(raw["namespace"] as? [String: Any])
            let config = try XCTUnwrap(namespace["config"] as? [String: Any])
            XCTAssertEqual(Set(response.namespace.config.keys), Set(config.keys))
            XCTAssertEqual(Set(response.namespace.fields.map(\.name)), Set(config.keys))
            XCTAssertEqual(response.namespace.fieldCount, response.namespace.fields.count)
            XCTAssertTrue(Set(response.namespace.config.keys)
                .isDisjoint(with: retiredFeatureFlags.union(retiredBillingFields)))
            XCTAssertTrue(Set(response.namespace.fields.map(\.name))
                .isDisjoint(with: retiredFeatureFlags.union(retiredBillingFields)))
        }
    }

    func testRetainedStripeBillingFieldPreservesBothBooleanValues() throws {
        for enabled in [true, false] {
            var publicObject = try XCTUnwrap(JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.feature-flags.default")
            ) as? [String: Any])
            var flags = try XCTUnwrap(publicObject["flags"] as? [String: Bool])
            flags["membershipStripeBilling"] = enabled
            publicObject["flags"] = flags
            let publicResponse = try JSONDecoder.vouchaFixtureDecoder.decode(
                FeatureFlagsResponse.self, from: JSONSerialization.data(withJSONObject: publicObject)
            )
            XCTAssertEqual(publicResponse.flags["membershipStripeBilling"], enabled)
            XCTAssertEqual(Set(publicResponse.flags.keys), Set(flags.keys))

            var updateObject = try XCTUnwrap(JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.dynamic-config.update.changed")
            ) as? [String: Any])
            var namespace = try XCTUnwrap(updateObject["namespace"] as? [String: Any])
            var config = try XCTUnwrap(namespace["config"] as? [String: Bool])
            config["membershipStripeBilling"] = enabled
            var fields = try XCTUnwrap(namespace["fields"] as? [[String: Any]])
            fields.removeAll { $0["name"] as? String == "membershipStripeBilling" }
            fields.append([
                "name": "membershipStripeBilling", "type": "boolean", "value": enabled,
                "default_value": false, "description": "Stripe billing"
            ])
            namespace["config"] = config
            namespace["fields"] = fields
            namespace["field_count"] = fields.count
            updateObject["namespace"] = namespace
            let update = try JSONDecoder.vouchaFixtureDecoder.decode(
                DynamicConfigUpdateResponse.self, from: JSONSerialization.data(withJSONObject: updateObject)
            )
            let field = try XCTUnwrap(update.namespace.fields.first { $0.name == "membershipStripeBilling" })
            XCTAssertEqual(field.type, .boolean)
            XCTAssertEqual(field.value, .boolean(enabled))
            XCTAssertEqual(update.namespace.config["membershipStripeBilling"], .boolean(enabled))
            XCTAssertEqual(Set(update.namespace.config.keys), Set(config.keys))
            XCTAssertEqual(
                try requestBodyJSON(from: Endpoint.updateDynamicConfigNamespace(
                    "feature-flags", field: field.name, value: .boolean(enabled)
                )),
                .object(["config": .object(["membershipStripeBilling": .bool(enabled)])])
            )
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
