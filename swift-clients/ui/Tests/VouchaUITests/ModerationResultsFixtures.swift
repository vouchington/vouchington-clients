import Foundation

enum ModerationResultsFixtures {
    static func page(
        status: String,
        ids: [String] = [],
        endCursor: String? = nil,
        hasNextPage: Bool = false
    ) -> Data {
        let rows = ids.map { id in
            """
            {
              "id": "\(id)",
              "post_id": "post-1",
              "community_prompt_id": "prompt-1",
              "is_flagged": false,
              "created_at": "2026-01-02T03:04:05.000Z"
            }
            """
        }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "results": [\(rows)],
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(cursor),
                "start_cursor": null
              },
              "platform_moderation": { "status": "\(status)" }
            }
            """.utf8
        )
    }
}
