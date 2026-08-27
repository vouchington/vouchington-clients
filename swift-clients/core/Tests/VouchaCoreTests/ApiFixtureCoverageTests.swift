import XCTest

/// Fixture-driven DTO field-completeness + consumer-coverage gates (#6773). Converts the
/// reactive "reviewer catches a missing response field" pattern (#6472, #6673, #6604, #6377)
/// into a local red test: every fixture in `ApiFixtureCoverage.registry` must round-trip every
/// field through its DTO, and every swift-core/swift-ui-consumed fixture must be registered.
final class ApiFixtureCoverageTests: XCTestCase {
    func testPrivateUserFixturePreservesRequiredContractFields() throws {
        let data = ApiFixtureLoader.data("swift.my.identity.default")

        let identity = try makeVouchaDecoder().decode(IdentityCoverageEnvelope.self, from: data).identity
        XCTAssertEqual(identity.entityType, "user")
        XCTAssertFalse(identity.isOfficialAccount)
        XCTAssertEqual(identity.cardsVisibility, .everyone)
        XCTAssertEqual(identity.directMessagesAudience, .users)
        XCTAssertEqual(identity.defaultPostPrivacy, "public")
        XCTAssertEqual(identity.moderationEmailDaysOfWeek, [1, 2, 3, 4, 5])
    }

    func testRegisteredFixturesRoundTripThroughTheirDTO() {
        for fixture in ApiFixtureCoverage.registry {
            XCTContext.runActivity(named: fixture.id) { _ in
                do {
                    try fixture.run(fixture.id)
                } catch {
                    XCTFail("\(fixture.id) failed fixture coverage: \(error)")
                }
            }
        }
    }

    func testEveryConsumedFixtureIsRegistered() {
        let consumed = Set(ApiFixtureLoader.swiftFixtureIdsWithBody)
        let registered = Set(ApiFixtureCoverage.registry.map(\.id))

        let unaccounted = consumed.subtracting(registered)
        XCTAssertTrue(
            unaccounted.isEmpty,
            "Fixtures consumed by swift-core/swift-ui but not registered: \(unaccounted.sorted())"
        )
    }
}
