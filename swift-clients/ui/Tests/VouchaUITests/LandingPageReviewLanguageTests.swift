import Foundation
@testable import VouchaFeatures
import XCTest

final class LandingPageReviewLanguageTests: XCTestCase {
    func testReviewLanguageKeysRequireExplicitNullWhenNoLanguageIsKnown() throws {
        let missingLanguageKeys = """
        {"id":"review-1","title":"Review","slug":null,"markdown":"Body",
        "created_at":"2026-01-01T00:00:00Z","review_topic_ratings":[]}
        """
        XCTAssertThrowsError(try decoder.decode(
            LandingPageReview.self,
            from: Data(missingLanguageKeys.utf8)
        ))

        let explicitNullLanguageKeys = """
        {"id":"review-1","title":"Review","slug":null,"markdown":"Body",
        "created_at":"2026-01-01T00:00:00Z","review_topic_ratings":[],
        "declared_language":null,"lingua_rs_detected_language":null}
        """
        let decoded = try decoder.decode(LandingPageReview.self, from: Data(explicitNullLanguageKeys.utf8))
        XCTAssertNil(decoded.declaredLanguage)
        XCTAssertNil(decoded.linguaRsDetectedLanguage)
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
