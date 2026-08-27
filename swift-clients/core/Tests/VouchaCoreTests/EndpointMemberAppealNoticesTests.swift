import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointMemberAppealNoticesTests: XCTestCase {
    func testNoticeEndpointsForwardOpaqueCursorsAndLimits() {
        let cases = [
            (Endpoint.memberWarningNotices(after: "warning/cursor", limit: 7), "/api/v1/my/warnings"),
            (Endpoint.memberCommunityBanNotices(after: "ban/cursor", limit: 8), "/api/v1/my/bans"),
            (Endpoint.memberRemovedPostNotices(after: "post/cursor", limit: 9), "/api/v1/my/removed-posts")
        ]

        for (endpoint, path) in cases {
            XCTAssertEqual(endpoint.path, path)
            XCTAssertEqual(endpoint.queryItems.last?.name, "after")
            XCTAssertTrue(endpoint.queryItems.last?.value?.hasSuffix("/cursor") == true)
        }
        XCTAssertEqual(cases[0].0.queryItems.first?.value, "7")
        XCTAssertEqual(cases[1].0.queryItems.first?.value, "8")
        XCTAssertEqual(cases[2].0.queryItems.first?.value, "9")
        XCTAssertTrue(cases[2].0.queryItems.contains(URLQueryItem(name: "include_platform", value: "true")))
    }

    func testWarningResponseDecodesCurrentLiveShapeAndMissingAdditiveFields() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberWarningNoticesResponse.self,
            from: Data("""
            {
              "warnings":[{
                "id":"warning-1",
                "user_id":"user-1",
                "community_id":null,
                "community_slug":null,
                "public_message":"Follow the rules.",
                "created_at":"2026-07-01T09:00:00Z"
              }],
              "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}
            }
            """.utf8)
        )

        let warning = try XCTUnwrap(response.warnings.first)
        XCTAssertEqual(warning.id, "warning-1")
        XCTAssertNil(warning.caseId)
        XCTAssertNil(warning.revokedAt)
    }

    func testBanAndRemovalResponsesDecodeCurrentLiveShapes() throws {
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let bans = try decoder.decode(
            MemberCommunityBanNoticesResponse.self,
            from: Data("""
            {"bans":[{
              "__entity_type":"community_ban","id":"ban-1","user_id":"user-1",
              "community_id":"community-1","community_slug":"community",
              "reason":null,"expires_at":null,"lifted_at":null,
              "created_at":"2026-07-01T09:00:00Z","updated_at":"2026-07-01T09:00:00Z"
            }],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
            """.utf8)
        )
        let removals = try decoder.decode(
            MemberRemovedPostNoticesResponse.self,
            from: Data("""
            {"removed_posts":[{
              "__entity_type":"removed_post","post_id":"post-1","post_title":null,
              "community_id":"community-1","community_slug":"community",
              "unpublished_at":"2026-07-01T09:00:00Z","post_removal_kind":"community"
            }],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
            """.utf8)
        )

        XCTAssertEqual(bans.bans.first?.id, "ban-1")
        XCTAssertEqual(removals.removedPosts.first?.entityType, "removed_post")
        XCTAssertEqual(removals.removedPosts.first?.postId, "post-1")
    }

    func testRemovalResponseDecodesPlatformNullCommunityAndKeepsKindsDistinct() throws {
        let removals = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberRemovedPostNoticesResponse.self,
            from: Data("""
            {"removed_posts":[
              {
                "__entity_type":"removed_post","post_id":"post-1","post_title":"Post",
                "community_id":"community-1","community_slug":"community",
                "unpublished_at":"2026-07-01T09:00:00Z","post_removal_kind":"community"
              },
              {
                "__entity_type":"removed_post","post_id":"post-1","post_title":"Post",
                "community_id":null,"community_slug":null,
                "unpublished_at":"2026-07-01T09:00:00Z","post_removal_kind":"platform"
              }
            ],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
            """.utf8)
        )

        XCTAssertNil(removals.removedPosts[1].communityId)
        XCTAssertEqual(
            Set(removals.removedPosts.map(\.id)),
            Set(["community:post-1", "platform:post-1"])
        )
    }
}
