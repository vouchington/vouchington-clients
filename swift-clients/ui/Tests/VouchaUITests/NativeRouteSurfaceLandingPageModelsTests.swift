import Foundation
@testable import VouchaFeatures
import XCTest

final class NativeRouteSurfaceLandingPageModelsTests: XCTestCase {
    func testRejectsLegacyPublicEnvelopeAndMissingItems() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        for json in [
            #"{"page":{"id":"page-1"}}"#,
            #"{"id":"page-1"}"#,
            #"{"landing_page":{"id":"page-1","title":"Home","slug":"home","is_default":true}}"#
        ] {
            XCTAssertThrowsError(try decoder.decode(NativePublicLandingPageResponse.self, from: Data(json.utf8)))
        }
    }
}
