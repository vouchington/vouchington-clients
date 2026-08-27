import Foundation
@testable import VouchaFeatures
import XCTest

final class NativeRouteSurfaceLandingPageModelsTests: XCTestCase {
    func testDecodesPublicLandingPageEnvelopeFromAllSupportedShapes() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase

        let landingPageEnvelope = try decoder.decode(
            NativePublicLandingPageResponse.self,
            from: Data("""
            {
              "landing_page": {
                "id": "page-1",
                "title": "Home",
                "subtitle": "Public profile",
                "slug": "home",
                "is_default": true
              }
            }
            """.utf8)
        )
        XCTAssertEqual(landingPageEnvelope.landingPage.id, "page-1")
        XCTAssertEqual(landingPageEnvelope.landingPage.subtitle, "Public profile")

        let pageEnvelope = try decoder.decode(
            NativePublicLandingPageResponse.self,
            from: Data("""
            {
              "page": {
                "id": "page-2",
                "title": "Docs",
                "subtitle": null,
                "slug": "docs",
                "is_default": false
              }
            }
            """.utf8)
        )
        XCTAssertEqual(pageEnvelope.landingPage.id, "page-2")
        XCTAssertNil(pageEnvelope.landingPage.subtitle)

        let bareLandingPage = try decoder.decode(
            NativePublicLandingPageResponse.self,
            from: Data("""
            {
              "id": "page-3",
              "title": "Launch",
              "subtitle": "Published page",
              "slug": "launch",
              "is_default": false
            }
            """.utf8)
        )
        XCTAssertEqual(bareLandingPage.landingPage.id, "page-3")
        XCTAssertEqual(bareLandingPage.landingPage.title, "Launch")
    }
}
