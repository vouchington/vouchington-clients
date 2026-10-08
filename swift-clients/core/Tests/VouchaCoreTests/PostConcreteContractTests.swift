import VouchaModels
import XCTest

final class PostConcreteContractTests: XCTestCase {
    func testPublicProvenanceAppFactsDecodeAndPreserveRequiredNull() throws {
        let verified = Data("""
        {"via":"mcp","app":{"kind":"verified","client_id":"agent-1","client_name":"Example"}}
        """.utf8)
        let decoded = try makeVouchaDecoder().decode(PublicContentProvenance.self, from: verified)
        XCTAssertEqual(decoded.app?.clientId, "agent-1")
        XCTAssertEqual(decoded.app?.clientName, "Example")

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(decoded)) as? [String: Any])
        let app = try XCTUnwrap(encoded["app"] as? [String: Any])
        XCTAssertEqual(app["client_id"] as? String, "agent-1")
        XCTAssertEqual(app["client_name"] as? String, "Example")
        XCTAssertNil(app["hostname"])

        let plain = try makeVouchaDecoder().decode(
            PublicContentProvenance.self, from: Data(#"{"via":"api","app":null}"#.utf8)
        )
        let encodedPlain = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(plain)) as? [String: Any])
        XCTAssertTrue(encodedPlain["app"] is NSNull)
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            PublicContentProvenance.self, from: Data(#"{"via":"api"}"#.utf8)
        ))
    }

    func testPostDecodesOptionalPublicContentProvenance() throws {
        let original = ApiFixtureLoader.data("native.bookmarks.posts.saved.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: original) as? [String: Any])
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])
        results[0]["provenance"] = [
            "via": "mcp",
            "app": ["kind": "verified", "client_id": "agent-1", "client_name": "Example"]
        ]
        object["results"] = results

        let labeled = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        results[0].removeValue(forKey: "provenance")
        object["results"] = results
        let unlabeled = try makeVouchaDecoder().decode(
            Page<Post>.self,
            from: JSONSerialization.data(withJSONObject: object)
        )

        XCTAssertEqual(
            labeled.results.first?.provenance,
            PublicContentProvenance(via: "mcp", app: PublicProvenanceApp(
                kind: "verified", clientId: "agent-1", clientName: "Example"
            ))
        )
        XCTAssertNil(unlabeled.results.first?.provenance)
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
