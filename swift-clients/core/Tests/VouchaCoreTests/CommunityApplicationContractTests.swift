import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CommunityApplicationContractTests: XCTestCase {
    func testDetailFixturePreservesAnswersAndNullableReviewState() throws {
        let id = "web.communities.application.default"
        try assertFixtureCoversDTO(id, as: CommunityApplicationResponse.self)
        let response = try makeVouchaDecoder().decode(
            CommunityApplicationResponse.self, from: ApiFixtureLoader.data(id)
        )
        XCTAssertEqual(response.communityApplication.entityType, "community_application")
        XCTAssertEqual(response.communityApplication.id, "application-1")
        XCTAssertEqual(response.communityApplication.message, "I would like to join.")
        XCTAssertTrue(response.communityApplication.answers.isEmpty)
        XCTAssertNil(response.communityApplication.reviewedAt)
        XCTAssertNil(response.communityApplication.rejectionReason)
    }

    func testDetailEndpointEncodesBothPathParameters() {
        assertEndpoint(
            .communityApplication(idOrSlug: "community/one%", applicationId: "application/two%"),
            path: "/api/v1/communities/community%2Fone%25/applications/application%2Ftwo%25"
        )
    }
}
