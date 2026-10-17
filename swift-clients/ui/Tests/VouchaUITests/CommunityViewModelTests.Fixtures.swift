import Foundation

extension CommunityViewModelTests {
    func registerDetailFixtures(membershipRole: String? = nil) {
        let membershipJSON = if let membershipRole {
            """
              "membership": {
                "id": "membership-1",
                "community_id": "community-1",
                "user_id": "user-1",
                "role": "\(membershipRole)",
                "approved_by_id": null,
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "removed_at": null,
                "removed_by_id": null
              },
            """
        } else {
            #""membership": null,"#
        }
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Builders",
                    "slug": "builders",
                    "markdown": "Native community",
                    "visibility": "public",
                    "member_roster_visibility": "public",
                    "list_type": null,
                    "member_invites_allowed_at": null,
                    "post_approval_required_at": null,
                    "automod_action": "record_only",
                    "should_allow_review_posts": true,
                    "should_allow_data_point_posts": true,
                    "trusted_at": null,
                    "profile_image_id": null,
                    "banner_image_id": null,
                    "created_by_id": "user-1",
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z",
                    "deleted_at": null,
                    "deleted_by_id": null,
                    "archived_at": null,
                    "archived_by_id": null,
                    "default_language": null,
                    "lingua_rs_detected_language": null,
                    "rules_markdown": null
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 1,
                    "post_count": 0,
                    "list_item_count": 0,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                  \(membershipJSON)
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(#"{"results":[],"posts":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            Data(#"{"questions":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/invites"] = (inviteListResponse(), 200)
    }

    func inviteListResponse() -> Data {
        Data("""
        {
          "results": [],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "community_invites": {}
        }
        """.utf8)
    }

    func inviteResponse(code: String? = nil) -> Data {
        let codeField = code.map { #""code": "\#($0)","# } ?? ""
        return Data("""
        {
          "community_invite": {
            "id": "invite-1",
            "community_id": "community-1",
            \(codeField)
            "invited_user_id": "user-1",
            "invited_email": null,
            "invited_by_id": "moderator-1",
            "accepted_at": null,
            "accepted_by_id": null,
            "declined_at": null,
            "revoked_at": null,
            "created_at": "2026-07-01T00:00:00Z"
          }
        }
        """.utf8)
    }
}
