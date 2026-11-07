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
        XCTAssertEqual(state.effective, ["fediverse": false, "memberships": true])
        for fixture in ["native.dynamic-config.update.changed", "native.dynamic-config.update.no-op"] {
            let update = try decoder.decode(DynamicConfigUpdateResponse.self, from: ApiFixtureLoader.data(fixture))
            state.applyGlobalNamespace(update.namespace)
            XCTAssertEqual(Set(state.effective.keys), Set(publicFlags.flags.keys))
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
}
