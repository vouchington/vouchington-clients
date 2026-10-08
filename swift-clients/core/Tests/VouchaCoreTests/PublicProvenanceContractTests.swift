import XCTest

final class PublicProvenanceContractTests: XCTestCase {
    /// The broad DTO parity suite runs separately without coverage in CI. These four newly
    /// required public contracts must also round-trip in the portable Core regression suite.
    func testPublicProvenanceResponsesPreserveTheirSerializedFields() {
        for fixture in entityProvenanceFixtureCoverage {
            XCTContext.runActivity(named: fixture.id) { _ in
                do {
                    try fixture.run(fixture.id)
                } catch {
                    XCTFail("Public provenance response failed to round-trip: \(fixture.id): \(error)")
                }
            }
        }
    }
}
