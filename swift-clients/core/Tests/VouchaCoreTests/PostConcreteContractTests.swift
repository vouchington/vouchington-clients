import VouchaModels
import XCTest

final class PostConcreteContractTests: XCTestCase {
    func testConcretePostSidecarsDecodeFromSharedFixtures() throws {
        let bookmarks = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: ApiFixtureLoader.data("native.bookmarks.posts.saved.default")
        )
        let post = try XCTUnwrap(bookmarks.results.first)

        XCTAssertEqual(post.entityType, "post")
        XCTAssertNil(post.approvedAt)
        XCTAssertNil(post.inReviewAt)
        XCTAssertNil(post.rejectedAt)
        XCTAssertNil(post.parentId)

        let profile = try makeVouchaDecoder().decode(
            UserProfilePostFeedResponse.self,
            from: ApiFixtureLoader.data("native.users.profile.posts.all")
        )
        let metrics = try XCTUnwrap(profile.postsMetrics["profile-review-1"])
        XCTAssertEqual(metrics.entityType, "post_metrics")
        XCTAssertEqual(metrics.id, "profile-review-1")
        XCTAssertNotNil(metrics.updatedAt)
        XCTAssertEqual(metrics.bookmarks, ["follow": 0, "save": 0])

        let election = try XCTUnwrap(profile.postElections["profile-review-1"])
        XCTAssertEqual(election.entityType, "post_election")
        XCTAssertEqual(election.id, "profile-review-1")
    }

    func testNewConcretePostFieldsRemainCompatibleWithOlderResponses() throws {
        let data = ApiFixtureLoader.data("native.bookmarks.posts.saved.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])

        for index in results.indices {
            results[index].removeValue(forKey: "__entity_type")
            results[index].removeValue(forKey: "approved_at")
            results[index].removeValue(forKey: "in_review_at")
            results[index].removeValue(forKey: "parent_id")
            results[index].removeValue(forKey: "rejected_at")
        }
        object["results"] = results

        let legacyData = try JSONSerialization.data(withJSONObject: object)
        let page = try makeVouchaDecoder().decode(Page<Post>.self, from: legacyData)
        let post = try XCTUnwrap(page.results.first)

        XCTAssertNil(post.entityType)
        XCTAssertNil(post.approvedAt)
        XCTAssertNil(post.inReviewAt)
        XCTAssertNil(post.parentId)
        XCTAssertNil(post.rejectedAt)
    }
}
