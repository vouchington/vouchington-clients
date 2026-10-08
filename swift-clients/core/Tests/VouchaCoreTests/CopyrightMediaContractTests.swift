@testable import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class CopyrightMediaContractTests: XCTestCase {
    func testPostImagePlacementFixturePreservesPlacementIdentity() throws {
        let response = try makeVouchaDecoder().decode(
            PostImagePlacementResponse.self,
            from: ApiFixtureLoader.data("native.posts.images.placement.default")
        )
        let image = try XCTUnwrap(response.images.first)
        XCTAssertEqual(image.placementId, "00000000-0000-7000-8000-000000000803")
        XCTAssertEqual(image.placementRevision, 1)
        XCTAssertEqual(image.imageId, "00000000-0000-7000-8000-000000000802")
    }

    func testSimilarityCandidateFixturePreservesAvailabilityAndPlacementIdentity() throws {
        let response = try makeVouchaDecoder().decode(
            CopyrightSimilarityCandidatesResponse.self,
            from: ApiFixtureLoader.data("native.moderation.copyright.image-similarity-candidates.default")
        )
        let candidate = try XCTUnwrap(response.copyrightImageSimilarityCandidates.first)
        XCTAssertEqual(response.availability, .available)
        XCTAssertEqual(candidate.placementId, "00000000-0000-7000-8000-000000000803")
        XCTAssertEqual(candidate.placementRevision, 1)
        XCTAssertEqual(candidate.similarity, 0.98, accuracy: 0.000_001)
    }

    func testCopyrightMediaEndpointsEscapeEveryPathSegment() {
        assertEndpoint(
            Endpoint.postImages(idOrSlug: "post / one"),
            path: "/api/v1/posts/post%20%2F%20one/images"
        )
        assertEndpoint(
            Endpoint.copyrightImageSimilarityCandidates(noticeId: "notice / one", targetId: "target / two"),
            path: "/api/v1/copyright-notices/notice%20%2F%20one/targets/target%20%2F%20two/image-similarity-candidates"
        )
    }

    func testPlacementImageURLUsesTheRevisionBoundDeliveryPath() throws {
        for (base, expectedBase) in [
            ("https://images.example.test", "https://images.example.test"),
            ("https://images.example.test/", "https://images.example.test"),
            ("https://images.example.test/assets", "https://images.example.test/assets"),
            ("https://images.example.test/assets///", "https://images.example.test/assets"),
            ("https://images.example.test/assets%20one/", "https://images.example.test/assets%20one")
        ] {
            let config = try AppConfig(
                baseURL: XCTUnwrap(URL(string: "https://api.example.test")),
                imageBaseURL: XCTUnwrap(URL(string: base))
            )
            XCTAssertEqual(
                config.imageURL(forPlacementId: "placement / one", revision: 0, imageId: "image / two", width: 960),
                "\(expectedBase)/images/placements/placement%20%2F%20one/0/image%20%2F%20two?w=960"
            )
            XCTAssertNil(config.imageURL(forPlacementId: "", revision: 1, imageId: "image"))
        }
    }
}
