import VouchaModels
import XCTest

final class PostConcreteContractTests: XCTestCase {
    func testPostDecodesOptionalPublicContentProvenance() throws {
        let original = ApiFixtureLoader.data("native.bookmarks.posts.saved.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: original) as? [String: Any])
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])
        results[0]["content_provenance"] = ["via": "mcp", "label": "via Example"]
        object["results"] = results

        let labeled = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        results[0].removeValue(forKey: "content_provenance")
        object["results"] = results
        let unlabeled = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: JSONSerialization.data(withJSONObject: object)
        )

        XCTAssertEqual(labeled.results.first?.contentProvenance, PublicContentProvenance(via: "mcp", label: "via Example"))
        XCTAssertNil(unlabeled.results.first?.contentProvenance)
    }

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
            results[index].removeValue(forKey: "parent_post_id")
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

    func testPostCategorySidecarsDecodeFromFeedResponses() throws {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.bookmarks.posts.saved.default")
        ) as? [String: Any])
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])
        results[0]["post_explicit_categories"] = [
            ["type": "topic", "topic_id": "topic-1", "topic_name": "Rewards"],
            ["type": "hashtag", "hashtag": "swift-ui"]
        ]
        results[0]["post_hashtags"] = [[
            "id": "hashtag-1",
            "key": "swift-ui",
            "display_token": "#Swift_UI",
            "topic_id": "topic-1"
        ]]
        object["results"] = results

        let page = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let post = try XCTUnwrap(page.results.first)

        XCTAssertEqual(post.postExplicitCategories?.first?.topicId, "topic-1")
        XCTAssertEqual(post.postExplicitCategories?.first?.topicName, "Rewards")
        XCTAssertEqual(post.postExplicitCategories?.last?.hashtag, "swift-ui")
        XCTAssertEqual(post.postHashtags?.first?.displayToken, "#Swift_UI")
        XCTAssertEqual(post.postHashtags?.first?.topicId, "topic-1")
    }
}
