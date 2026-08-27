@testable import VouchaAPI
import XCTest

final class EndpointRouteParameterEncodingTests: XCTestCase {
    func testRouteDerivedValuesRemainSingleEncodedPathSegments() {
        XCTAssertEqual(
            Endpoint.userFollowing(userId: "user/one%").path,
            "/api/v1/users/user%2Fone%25/users/following"
        )
        XCTAssertEqual(
            Endpoint.myConversationMessages(conversationId: "conversation/one%").path,
            "/api/v1/my/conversations/conversation%2Fone%25/messages"
        )
        XCTAssertEqual(
            Endpoint.appeal(id: "appeal/one%").path,
            "/api/v1/appeals/appeal%2Fone%25"
        )
        XCTAssertEqual(
            Endpoint.publicLandingPage(username: "user/one%", slug: "landing/two%").path,
            "/api/v1/users/user%2Fone%25/landing-pages/landing%2Ftwo%25"
        )
        XCTAssertEqual(Endpoint.post(idOrSlug: "post/one%").path, "/api/v1/posts/post%2Fone%25")
        XCTAssertEqual(Endpoint.topic(id: "topic/one%").path, "/api/v1/topics/topic%2Fone%25")
        XCTAssertEqual(
            Endpoint.topicRecommendation(id: "recommendation/one%").path,
            "/api/v1/topic-recommendations/recommendation%2Fone%25"
        )
        XCTAssertEqual(
            Endpoint.updateTopicRecommendation(id: "recommendation/one%", body: ["title": "Updated"]).path,
            "/api/v1/topic-recommendations/recommendation%2Fone%25"
        )
    }
}
