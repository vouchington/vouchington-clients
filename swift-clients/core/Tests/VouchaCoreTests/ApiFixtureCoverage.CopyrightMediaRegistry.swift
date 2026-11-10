@testable import VouchaAPI
import VouchaModels
import XCTest

let copyrightMediaFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.posts.images.placement-limit.conflict") {
        let payload = try APIClient.makeDecoder().decode(APIErrorPayload.self, from: ApiFixtureLoader.data($0))
        XCTAssertEqual(payload.code, "IMAGE_PLACEMENT_LIMIT_REACHED")
        XCTAssertEqual(payload.displayMessage, "Image placement limit reached")
    },
    RegisteredFixture(id: "native.posts.images.placement.default") {
        try assertFixtureCoversDTO($0, as: PostImagePlacementResponse.self)
    },
    RegisteredFixture(id: "native.moderation.copyright.image-similarity-candidates.default") {
        try assertFixtureCoversDTO($0, as: CopyrightSimilarityCandidatesResponse.self)
    }
]
