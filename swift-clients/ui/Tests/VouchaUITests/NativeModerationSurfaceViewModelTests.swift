import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeModerationSurfaceViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testReportsRouteLoadsModerationReports() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (reportsData, 200)
        let viewModel = try ModerationReportsViewModel(
            client: makeClient(),
            viewerTier: .administrator
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/reports")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&status=pending&cluster=entity")
        XCTAssertEqual(viewModel.duplicateClusters.first?.signal, "content_hash_duplicate")
        XCTAssertEqual(viewModel.clusters.first?.targetLabel, "Native post")
    }

    func testModerationAdminRoutesLoadModlogAndAnalytics() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/admin/modlog"] = (modlogData, 200)
        let modlogRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/modlog")?.match)
        let modlogViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationAdmin),
            client: makeClient(),
            routeMatch: modlogRoute
        )

        await modlogViewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/admin/modlog")
        XCTAssertEqual(
            modlogViewModel.rows.first,
            verbatimRow(icon: "clock.arrow.circlepath", title: "ban", detail: "alice · Native Community")
        )

        CannedFeedURLProtocol.handlers["/api/v1/admin/moderation-analytics"] = (analyticsData, 200)
        let analyticsRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/moderation-analytics")?.match)
        let analyticsViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationAdmin),
            client: makeClient(),
            routeMatch: analyticsRoute
        )

        await analyticsViewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/admin/moderation-analytics")
        XCTAssertEqual(analyticsViewModel.rows.first?.title, "Queue volume")
    }

    func testDisputesRouteUsesLifecycleFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (disputesData, 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/disputes")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationDisputes),
            client: makeClient(),
            routeMatch: route
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/disputes")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "exclamationmark.bubble", title: "Native review dispute", detail: "Pending · Remove")
        )
    }

    private var reportsData: Data {
        Data("""
        {
          "cluster_mode": "entity",
          "results": [{
            "id": "cluster-1",
            "entity_type": "post",
            "entity_id": "post-1",
            "report_count": 3,
            "reporter_count": 2,
            "reason_breakdown": [{ "reason": "spam", "count": 3 }],
            "first_reported_at": "2026-01-01T00:00:00Z",
            "last_reported_at": "2026-01-02T00:00:00Z",
            "target_label": "Native post",
            "target_path": "/discussion/post-1",
            "target_content": null,
            "admin_action_path": "/admin/posts/post-1",
            "target_user_id": "target-1",
            "target_available": true,
            "target_is_restricted": false,
            "indicators": {
              "content_hash_duplicate": true,
              "embeddings_similarity": false,
              "velocity_spike": false
            },
            "reports": []
          }],
          "duplicate_clusters": [{
            "id": "duplicate-1",
            "signal": "content_hash_duplicate",
            "post_count": 1,
            "report_count": 3,
            "reason_breakdown": [{ "reason": "spam", "count": 3 }],
            "first_reported_at": "2026-01-01T00:00:00Z",
            "last_reported_at": "2026-01-02T00:00:00Z",
            "clusters": []
          }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }

    private var modlogData: Data {
        Data("""
        {
          "results": [{ "id": "action-1" }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "moderator_actions": {
            "action-1": {
              "id": "action-1",
              "community_id": "community-1",
              "actor_id": "actor-1",
              "action_type": "ban",
              "post_id": null,
              "target_user_id": "target-1",
              "report_id": null,
              "review_dispute_id": null,
              "community_application_id": null,
              "reason": "Native Community",
              "metadata": {},
              "created_at": "2026-01-01T00:00:00Z"
            }
          },
          "users": {
            "actor-1": {
              "id": "actor-1",
              "username": "alice",
              "roles": [],
              "display_name": "Alice",
              "avatar_url": null
            }
          }
        }
        """.utf8)
    }

    private var analyticsData: Data {
        Data("""
        {
          "range": "30d",
          "period_start": "2026-01-01T00:00:00Z",
          "period_end": "2026-01-31T00:00:00Z",
          "scope": { "type": "global" },
          "queue_volume": {
            "total_reports": 1,
            "pending_reports": 1,
            "reports_over_time": [],
            "clearance_actions_over_time": [],
            "moderator_actions_over_time": []
          },
          "rule_violations": { "reasons": [], "reasons_over_time": [] },
          "automod_performance": {
            "total_actions": 0,
            "auto_removes": 0,
            "reviewed_count": 0,
            "false_positive_count": 0,
            "false_positive_rate": null,
            "actions_over_time": [],
            "confidence_distribution": [],
            "sources": []
          },
          "moderator_workload": { "moderators": [], "users": {} },
          "appeals": {
            "total_closed": 0,
            "accepted": 0,
            "reduced": 0,
            "denied": 0,
            "dismissed": 0,
            "success_rate": null
          },
          "new_user_friction": {
            "first_posts": 0,
            "rejected_first_posts": 0,
            "rejection_rate": null
          }
        }
        """.utf8)
    }

    private var disputesData: Data {
        Data("""
        {
          "disputes": [{
            "id": "dispute-1",
            "post_id": "post-1",
            "topic_id": "topic-1",
            "disputant_user_id": "user-1",
            "reason": "other",
            "claim_text": "Native review dispute",
            "status": "pending",
            "recommended_action": "remove",
            "is_overdue": false,
            "ai_public_response": null,
            "ai_internal_response": null,
            "model": null,
            "ai_drafted_at": null,
            "public_response": null,
            "internal_notes": null,
            "drafted_at": null,
            "edited_at": null,
            "edited_by_id": null,
            "approved_at": null,
            "approved_by_id": null,
            "sent_at": null,
            "resolved_at": null,
            "resolved_by_id": null,
            "resolution_action": null,
            "latest_lifecycle_change_id": null,
            "post_content": null,
            "created_at": "2026-01-01T00:00:00Z",
            "updated_at": "2026-01-01T00:00:00Z"
          }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }
}
