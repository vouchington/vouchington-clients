import Foundation
import ViewInspector
import VouchaAPI
import VouchaDesignSystem
import VouchaModels
import XCTest

@MainActor
final class AuthoredPostStatusMatrixTests: XCTestCase {
    private let supportedTypes = [
        "discussion", "review", "data_point", "comment", "article",
        "blog_post", "story", "link", "topic_recommendation"
    ]

    func testEverySupportedTypeHidesRejectedBodyFromOtherViewers() throws {
        for type in supportedTypes {
            let post = try fixturePost(type: type, status: "rejected")
            let other = PostCard(post: post, variant: .expanded, currentUserId: "other")
            let author = PostCard(post: post, variant: .expanded, currentUserId: "user-1")
            let staff = PostCard(post: post, variant: .expanded, currentUserId: "other", isStaffViewer: true)

            XCTAssertNoThrow(try other.inspect().find(text: "This content is unavailable."), type)
            XCTAssertThrowsError(try other.inspect().find(text: "Authored profile review"), type)
            XCTAssertThrowsError(try other.inspect().find(button: "View appeals"), type)
            XCTAssertNoThrow(try author.inspect().find(text: "This content was removed by a moderator."), type)
            XCTAssertThrowsError(try author.inspect().find(text: "Authored profile review"), type)
            XCTAssertNoThrow(try staff.inspect().find(text: "In review"), type)
            XCTAssertNoThrow(try staff.inspect().find(text: "Authored profile review"), type)
        }
    }

    func testLateRejectionAndRecoveryReplaceNoticeWithoutClaimingPublicEligibility() throws {
        let states = ["pending", "in_review", "approved", "rejected", "approved"]
        for status in states {
            let post = try fixturePost(type: "review", status: status)
            XCTAssertEqual(post.id, "profile-review-1")
            let author = PostCard(post: post, variant: .expanded, currentUserId: "user-1")
            let other = PostCard(post: post, variant: .expanded, currentUserId: "other")

            if status == "rejected" {
                XCTAssertNoThrow(try author.inspect().find(text: "This content was removed by a moderator."))
                XCTAssertNoThrow(try other.inspect().find(text: "This content is unavailable."))
                XCTAssertThrowsError(try other.inspect().find(text: "Authored profile review"))
            } else {
                XCTAssertNoThrow(try author.inspect().find(text: "Authored profile review"), status)
                XCTAssertNoThrow(try other.inspect().find(text: "Authored profile review"), status)
                XCTAssertThrowsError(
                    try author.inspect().find(text: "This content was removed by a moderator."),
                    status
                )
                XCTAssertThrowsError(try other.inspect().find(text: "This content is unavailable."), status)
            }
        }
    }

    private func fixturePost(type: String, status: String) throws -> Post {
        let fixture = ApiFixtureLoader.data("native.users.profile.posts.all")
        let body = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        let posts = try XCTUnwrap(body["posts"] as? [String: [String: Any]])
        var post = try XCTUnwrap(posts["profile-review-1"])
        post["post_type"] = type
        post["clearance_status"] = status
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            Post.self,
            from: JSONSerialization.data(withJSONObject: post)
        )
    }
}
