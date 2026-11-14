import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import VouchaPersistence
import XCTest

@MainActor
final class RetiredFeatureFlagOverrideTests: XCTestCase {
    func testLandedPublicAndGlobalFlagsDoNotResurrectRetiredDeviceOverrides() throws {
        let retired = [
            "membershipAppleBilling", "membershipGoogleBilling", "membershipMicrosoftBilling",
            "chat", "support", "combinedSearch"
        ]
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = FeatureFlagOverrideFileStore(fileURL: directory.appendingPathComponent("overrides.json"))
        for key in retired {
            try store.set(true, for: key)
        }
        try store.set(true, for: "memberships")
        let state = FeatureFlagState(store: store)
        XCTAssertTrue(state.effective.isEmpty)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let publicFlags = try decoder.decode(
            FeatureFlagsResponse.self, from: ApiFixtureLoader.data("native.feature-flags.default")
        )
        XCTAssertTrue(state.applyPublicResponse(publicFlags, for: state.beginPublicFetch()))
        XCTAssertEqual(Set(state.effective.keys), Set(publicFlags.flags.keys))
        XCTAssertEqual(state.effective["fediverse"], false)
        XCTAssertEqual(state.effective["memberships"], true)
        for fixture in ["native.dynamic-config.update.changed", "native.dynamic-config.update.no-op"] {
            let update = try decoder.decode(DynamicConfigUpdateResponse.self, from: ApiFixtureLoader.data(fixture))
            state.applyGlobalNamespace(update.namespace)
            XCTAssertEqual(Set(state.effective.keys), Set(update.namespace.config.keys))
            XCTAssertEqual(state.effective["memberships"], true)
            XCTAssertEqual(state.effective["fediverse"], update.namespace.config["fediverse"] == .boolean(true))
            for key in retired {
                XCTAssertNil(state.effective[key])
                XCTAssertNil(state.localOverride(for: key))
                XCTAssertThrowsError(try state.setLocalOverride(true, for: key)) {
                    XCTAssertEqual($0 as? FeatureFlagOverrideError, .unknownFlag(key))
                }
            }
        }
    }

    func testRetainedStripePublicAndGlobalValuesSupportDeviceOverrides() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let state = FeatureFlagState(store: FeatureFlagOverrideFileStore(
            fileURL: directory.appendingPathComponent("overrides.json")
        ))
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        for enabled in [true, false] {
            var publicObject = try XCTUnwrap(JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.feature-flags.default")
            ) as? [String: Any])
            var flags = try XCTUnwrap(publicObject["flags"] as? [String: Bool])
            flags["membershipStripeBilling"] = enabled
            publicObject["flags"] = flags
            let response = try decoder.decode(
                FeatureFlagsResponse.self, from: JSONSerialization.data(withJSONObject: publicObject)
            )
            XCTAssertTrue(state.applyPublicResponse(response, for: state.beginPublicFetch()))
            try assertStripeOverrideBehavior(state, enabled: enabled)

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
            let update = try decoder.decode(
                DynamicConfigUpdateResponse.self, from: JSONSerialization.data(withJSONObject: updateObject)
            )
            state.applyGlobalNamespace(update.namespace)
            try assertStripeOverrideBehavior(state, enabled: enabled)
        }
    }

    private func assertStripeOverrideBehavior(_ state: FeatureFlagState, enabled: Bool) throws {
        XCTAssertEqual(state.remote["membershipStripeBilling"], enabled)
        XCTAssertEqual(state.effective["membershipStripeBilling"], enabled)
        try state.setLocalOverride(!enabled, for: "membershipStripeBilling")
        XCTAssertEqual(state.localOverride(for: "membershipStripeBilling"), !enabled)
        XCTAssertEqual(state.effective["membershipStripeBilling"], !enabled)
        try state.removeLocalOverride(for: "membershipStripeBilling")
        XCTAssertNil(state.localOverride(for: "membershipStripeBilling"))
        XCTAssertEqual(state.effective["membershipStripeBilling"], enabled)
    }

}
