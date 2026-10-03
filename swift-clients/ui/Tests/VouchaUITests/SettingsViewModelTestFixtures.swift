import Foundation

protocol CompleteSettingsResponseSeeding {}

extension SettingsViewModelTests: CompleteSettingsResponseSeeding {}
extension SettingsViewModelBlueskyTests: CompleteSettingsResponseSeeding {}

@MainActor
extension CompleteSettingsResponseSeeding {
    func seedSettingsResponses() {
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants([]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                profileImageId: "image-1",
                overrides: [
                    "use_display_name_from": "github",
                    "cards_visibility": "users",
                    "rewards_program_statuses_visibility": "users",
                    "spending_categories_visibility": "nobody",
                    "follows_visibility": "followers",
                    "community_memberships_visibility": "users",
                    "followers_visibility": "followers",
                    "ui_locale": "en",
                    "default_post_broadcast": "followers",
                    "default_post_privacy": "private",
                    "processing_restricted_at": "2026-03-01T00:00:00Z",
                    "third_party_marketing": true
                ]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Native bio"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data("""
            {
              "results": [
                {
                  "id": "link-1",
                  "user_id": "user-1",
                  "link_type": "github",
                  "sort_order": 0,
                  "url_id": null,
                  "url": null,
                  "handle": "alice",
                  "name": "Alice",
                  "image_id": null,
                  "created_at": "2026-03-01T10:00:00Z",
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data("""
            {
              "results": [
                {
                  "id": "key-1",
                  "user_id": "user-1",
                  "prefix": "voucha_rss_abcd",
                  "type": "rss",
                  "label": "Reader",
                  "permissions": ["rss-feeds:read"],
                  "created_at": "2026-03-01T10:00:00Z",
                  "last_used_at": null,
                  "revoked_at": null,
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/push-subscriptions"] = (
            Data("""
            {
              "results": [
                {
                  "id": "sub-1",
                  "user_id": "user-1",
                  "endpoint": "https://push.example.com",
                  "p256dh": "abcdabcdabcdabcd",
                  "auth": "abcdefgh",
                  "expiration_time_ms": null,
                  "user_agent": "Safari",
                  "last_success_at": null,
                  "last_failure_at": null,
                  "created_at": "2026-03-01T10:00:00Z",
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data("""
            {
              "membership": {
                "id": "mem-1",
                "user_id": "user-1",
                "plan": "pro",
                "status": "active",
                "started_at": "2026-03-01T10:00:00Z",
                "expires_at": null,
                "granted_by_id": null,
                "cancelled_at": null,
                "expired_at": null,
                "past_due_at": null,
                "paused_at": null,
                "cancel_at_period_end": false,
                "latest_change_id": null,
                "created_at": "2026-03-01T10:00:00Z",
                "updated_at": "2026-03-01T10:00:00Z",
                "sku": {
                  "id": "sku-1",
                  "plan": "pro",
                  "price": { "amount": 1299, "currency": "usd" },
                  "interval": "monthly",
                  "stripe_price_id": "price-pro",
                  "retired_at": null
                },
                "has_stripe_subscription": true
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/plans"] = (
            Data("""
            {
              "products": [
                  {
                    "id": "sku-1",
                    "plan": "plus",
                    "interval": "monthly",
                    "providers": [{ "provider": "stripe", "environment": "test", "application_id": "voucha-web", "product_id": "price-plus-monthly", "base_plan_id": null, "offer_id": null, "sku_id": null, "price": { "amount": 1299, "currency": "usd" } }]
                  },
                  {
                    "id": "sku-2",
                    "plan": "plus",
                    "interval": "yearly",
                    "providers": [{ "provider": "stripe", "environment": "test", "application_id": "voucha-web", "product_id": "price-plus-yearly", "base_plan_id": null, "offer_id": null, "sku_id": null, "price": { "amount": 9999, "currency": "usd" } }]
                  },
                  {
                    "id": "sku-3",
                    "plan": "pro",
                    "interval": "monthly",
                    "providers": [{ "provider": "stripe", "environment": "test", "application_id": "voucha-web", "product_id": "price-pro-monthly", "base_plan_id": null, "offer_id": null, "sku_id": null, "price": { "amount": 2499, "currency": "usd" } }]
                  }
              ],
              "benefit_catalog": {
                "version": 1,
                "groups": [
                  {
                    "id": "contribute",
                    "benefits": [
                      {
                        "id": "public_contribution_access",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "access", "access": "after_wait" },
                          "plus": { "kind": "access", "access": "immediate" },
                          "pro": { "kind": "access", "access": "immediate" }
                        }
                      },
                      {
                        "id": "contribution_capacity",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "level", "level": "standard" },
                          "plus": { "kind": "level", "level": "more" },
                          "pro": { "kind": "level", "level": "most" }
                        }
                      },
                      {
                        "id": "automatic_post_topics",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "level", "level": "none" },
                          "plus": { "kind": "level", "level": "more" },
                          "pro": { "kind": "level", "level": "most" }
                        }
                      }
                    ]
                  },
                  {
                    "id": "research",
                    "benefits": [
                      {
                        "id": "ugc_downvote_counts",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "availability", "included": false },
                          "plus": { "kind": "availability", "included": true },
                          "pro": { "kind": "availability", "included": true }
                        }
                      }
                    ]
                  },
                  {
                    "id": "communities",
                    "benefits": [
                      {
                        "id": "community_agent_rules",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "quantity", "quantity": 0 },
                          "plus": { "kind": "quantity", "quantity": 3 },
                          "pro": { "kind": "quantity", "quantity": 10 }
                        }
                      }
                    ]
                  },
                  {
                    "id": "support",
                    "benefits": [
                      {
                        "id": "support_service_level",
                        "placements": ["card", "comparison"],
                        "values": {
                          "free": { "kind": "level", "level": "standard" },
                          "plus": { "kind": "level", "level": "priority" },
                          "pro": { "kind": "level", "level": "highest_priority" }
                        }
                      }
                    ]
                  }
                ]
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/data-request"] = (
            Data("""
            {
              "id": "req-1",
              "status": "ready",
              "created_at": "2026-03-01T10:00:00Z",
              "expires_at": "2027-03-08T10:00:00Z",
              "download_url": "https://example.com/export.zip"
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions"] = (
            Data("""
            {
              "results": [
                {
                  "id": "session-1",
                  "device_id": "device-1",
                  "device_name": "MacBook Pro",
                  "user_agent": "Safari",
                  "ip_address": "203.0.113.8",
                  "created_at": "2026-03-01T10:00:00Z",
                  "last_seen_at": "2026-03-01T12:00:00Z",
                  "expires_at": "2026-03-31T10:00:00Z",
                  "is_current": true
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
    }
}
