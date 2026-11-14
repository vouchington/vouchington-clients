@testable import VouchaModels
import XCTest

final class StoryDiscussionResponseDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        d.dateDecodingStrategy = .iso8601
        return d
    }()

    func testDecodesStoryPostResult() throws {
        let json = Data("""
        {
          "post": {
            "id": "post-1",
            "slug": "story-post",
            "post_type": "story",
            "title": "Story title",
            "markdown": "",
            "html": null,
            "parent_post_id": null,
            "root_post_id": null,
            "created_by_id": "story-teller",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": "approved",
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)

        let response = try decoder.decode(StoryPostResult.self, from: json)
        XCTAssertEqual(response.post.id, "post-1")
        XCTAssertEqual(response.post.postType, .story)
    }
}
