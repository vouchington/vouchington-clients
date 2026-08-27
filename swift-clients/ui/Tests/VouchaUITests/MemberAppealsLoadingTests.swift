import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealsLoadingTests: NativeRouteSurfaceViewModelTestCase {
    func testMapsEveryDedicatedMemberRoute() {
        XCTAssertEqual(MemberAppealsRoute(path: "/my/appeals"), .tracking)
        XCTAssertEqual(MemberAppealsRoute(path: "/my/warnings"), .warnings)
        XCTAssertEqual(MemberAppealsRoute(path: "/my/bans"), .bans)
        XCTAssertEqual(MemberAppealsRoute(path: "/my/removed-posts"), .removedPosts)
        XCTAssertEqual(MemberAppealsRoute(path: "/my/account-status"), .suspension)
        XCTAssertNil(MemberAppealsRoute(path: "/appeals"))
    }

    func testTargetIdentityDeduplicatesPerNotice() {
        let date = Date(timeIntervalSince1970: 1)
        let targets: Set<MemberAppealTarget> = [
            .warning(id: "w1", message: nil, community: nil, createdAt: date),
            .warning(id: "w1", message: nil, community: nil, createdAt: date),
            .ban(id: "b1", reason: nil, community: nil, createdAt: date)
        ]
        XCTAssertEqual(targets.count, 2)
        XCTAssertEqual(targets.map(\.id).sorted(), ["ban:b1", "warning:w1"])
    }

    func testRouteFiltersEligibleTargets() {
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        let suspensionDate = Date(timeIntervalSince1970: 1)
        viewModel.suspensionDate = suspensionDate
        let expectedId = MemberAppealTarget.suspension(date: suspensionDate).id
        XCTAssertEqual(viewModel.eligibleTargets.map(\.id), [expectedId])
    }

    func testRevokedWarningsAreNotEligibleAppealTargets() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(activeId: "active", revokedId: "revoked"), 200
        )
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.eligibleTargets.map(\.id), ["warning:active"])
    }

    func testPendingRemovalMatchesOnlyTheSameRemovalKind() throws {
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .removedPosts,
            draftStore: MemberAppealDraftStore()
        )
        let date = Date(timeIntervalSince1970: 1)
        let community = MemberAppealTarget.removal(
            id: "post-1", title: nil, community: nil, kind: .community, date: date
        )
        let platform = MemberAppealTarget.removal(
            id: "post-1", title: nil, community: nil, kind: .platform, date: date
        )

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let response = try decoder.decode(
            ModerationAppealListResponse.self,
            from: ModerationAppealsTestSupport.list([
                ModerationAppealsTestSupport.appeal(
                    postId: "post-1",
                    postRemovalKind: "community"
                )
            ])
        )
        viewModel.pendingAppealPagination.reset(items: [response.appeals[0]])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)

        XCTAssertFalse(viewModel.canAppeal(community))
        XCTAssertTrue(viewModel.canAppeal(platform))
    }

    func testPendingTargetCannotBeOpenedTwiceWhileSubmitting() {
        let target = MemberAppealTarget.warning(
            id: "w1", message: "Notice", community: "community", createdAt: .now
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        viewModel.submissionState = .submitting
        XCTAssertFalse(viewModel.canAppeal(target))
        viewModel.beginAppeal(target)
        XCTAssertNil(viewModel.activeTarget)
    }

    func testTrackingLoadsEveryAppealStatusWithIndependentRequests() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([ModerationAppealsTestSupport.appeal()]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (emptyPage("warnings"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/bans"] = (emptyPage("bans"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/removed-posts"] = (emptyPage("removed_posts"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .tracking,
            draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        let statusQueries = Set(CannedFeedURLProtocol.capturedURLs
            .filter { $0.path == "/api/v1/appeals" }
            .compactMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }
            .compactMap { components in
                components.queryItems?.first(where: { $0.name == "status" })?.value
            })
        XCTAssertEqual(statusQueries, Set(["pending", "resolved", "dismissed"]))
        XCTAssertTrue(viewModel.pendingAppealPagination.hasLoadedPage)
        XCTAssertTrue(viewModel.resolvedAppealPagination.hasLoadedPage)
        XCTAssertTrue(viewModel.dismissedAppealPagination.hasLoadedPage)
    }

    func testTrackingPreservesSuccessfulStreamsWhenAnotherRequestFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([ModerationAppealsTestSupport.appeal()]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(activeId: "active", revokedId: "revoked"), 200
        )
        CannedFeedURLProtocol.errors["/api/v1/my/bans"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.handlers["/api/v1/my/removed-posts"] = (
            removalPage(postId: "post-1"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                overrides: ["suspended_at": "2026-07-01T12:00:00Z"]
            ),
            200
        )
        let viewModel = try makeViewModel(route: .tracking)

        await viewModel.load()

        XCTAssertEqual(viewModel.appeals.count, 1)
        try XCTAssertEqual(
            Set(viewModel.eligibleTargets.map(\.id)),
            Set([
                "warning:active",
                "removal:platform:post-1",
                suspensionTargetId(for: viewModel)
            ])
        )
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testNoticeRouteLoadsOnlyPendingAppeals() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (emptyPage("warnings"), 200)
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        let statusQueries = CannedFeedURLProtocol.capturedURLs
            .filter { $0.path == "/api/v1/appeals" }
            .compactMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }
            .compactMap { components in
                components.queryItems?.first(where: { $0.name == "status" })?.value
            }
        XCTAssertEqual(statusQueries, ["pending"])
        XCTAssertTrue(viewModel.pendingAppealPagination.hasLoadedPage)
        XCTAssertFalse(viewModel.resolvedAppealPagination.hasLoadedPage)
        XCTAssertFalse(viewModel.dismissedAppealPagination.hasLoadedPage)
    }

    func testDedicatedBanRemovalAndSuspensionRoutesLoadTheirTargets() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/bans"] = (
            banPage(id: "ban-1"), 200
        )
        let bans = try makeViewModel(route: .bans)
        await bans.load()
        XCTAssertEqual(bans.eligibleTargets.map(\.id), ["ban:ban-1"])

        CannedFeedURLProtocol.handlers["/api/v1/my/removed-posts"] = (
            removalPage(postId: "post-1"), 200
        )
        let removals = try makeViewModel(route: .removedPosts)
        await removals.load()
        XCTAssertEqual(removals.eligibleTargets.map(\.id), ["removal:platform:post-1"])

        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                overrides: ["suspended_at": "2026-07-01T12:00:00Z"]
            ),
            200
        )
        let suspension = try makeViewModel(route: .suspension)
        await suspension.load()
        try XCTAssertEqual(suspension.eligibleTargets.map(\.id), [suspensionTargetId(for: suspension)])
    }

    func testDedicatedRoutesPreserveNoticeWhenPendingAppealsFail() async throws {
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.notConnectedToInternet)

        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(activeId: "warning", revokedId: "revoked"), 200
        )
        let warnings = try makeViewModel(route: .warnings)
        await warnings.load()
        XCTAssertEqual(warnings.eligibleTargets.map(\.id), ["warning:warning"])
        XCTAssertNotNil(warnings.errorMessage)

        CannedFeedURLProtocol.handlers["/api/v1/my/bans"] = (banPage(id: "ban"), 200)
        let bans = try makeViewModel(route: .bans)
        await bans.load()
        XCTAssertEqual(bans.eligibleTargets.map(\.id), ["ban:ban"])
        XCTAssertNotNil(bans.errorMessage)

        CannedFeedURLProtocol.handlers["/api/v1/my/removed-posts"] = (
            removalPage(postId: "post"), 200
        )
        let removals = try makeViewModel(route: .removedPosts)
        await removals.load()
        XCTAssertEqual(removals.eligibleTargets.map(\.id), ["removal:platform:post"])
        XCTAssertNotNil(removals.errorMessage)

        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                overrides: ["suspended_at": "2026-07-01T12:00:00Z"]
            ),
            200
        )
        let suspension = try makeViewModel(route: .suspension)
        await suspension.load()
        try XCTAssertEqual(suspension.eligibleTargets.map(\.id), [suspensionTargetId(for: suspension)])
        XCTAssertNotNil(suspension.errorMessage)
    }

    func testDedicatedRoutePreservesPendingAppealsWhenNoticeFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([ModerationAppealsTestSupport.appeal()]),
            200
        )
        CannedFeedURLProtocol.errors["/api/v1/my/warnings"] = URLError(.notConnectedToInternet)
        let viewModel = try makeViewModel(route: .warnings)

        await viewModel.load()

        XCTAssertEqual(viewModel.appeals.map(\.id), ["appeal-1"])
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testEveryAppealStatusLoadsItsOwnNextPage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (ModerationAppealsTestSupport.list([
                ModerationAppealsTestSupport.appeal(id: "pending-2")
            ]), 200, 0),
            (ModerationAppealsTestSupport.list([
                ModerationAppealsTestSupport.appeal(id: "resolved-2", status: "resolved")
            ]), 200, 0),
            (ModerationAppealsTestSupport.list([
                ModerationAppealsTestSupport.appeal(id: "dismissed-2", status: "dismissed")
            ]), 200, 0)
        ]
        let viewModel = try makeViewModel(route: .tracking)
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: "pending", hasMore: true)
        viewModel.resolvedAppealPagination.restoreContinuation(endCursor: "resolved", hasMore: true)
        viewModel.dismissedAppealPagination.restoreContinuation(endCursor: "dismissed", hasMore: true)

        await viewModel.loadMoreAppeals(status: .pending)
        await viewModel.loadMoreAppeals(status: .resolved)
        await viewModel.loadMoreAppeals(status: .dismissed)

        XCTAssertEqual(viewModel.pendingAppealPagination.items.map(\.id), ["pending-2"])
        XCTAssertEqual(viewModel.resolvedAppealPagination.items.map(\.id), ["resolved-2"])
        XCTAssertEqual(viewModel.dismissedAppealPagination.items.map(\.id), ["dismissed-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.compactMap(\.query),
            [
                "limit=25&status=pending&after=pending&mine=true",
                "limit=25&status=resolved&after=resolved&mine=true",
                "limit=25&status=dismissed&after=dismissed&mine=true"
            ]
        )
    }

    func testEveryNoticeKindLoadsItsOwnNextPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            warningPage(activeId: "warning-2", revokedId: "warning-revoked"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/bans"] = (banPage(id: "ban-2"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/removed-posts"] = (
            removalPage(postId: "post-2"), 200
        )
        let viewModel = try makeViewModel(route: .tracking)
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)
        viewModel.warningPagination.restoreContinuation(endCursor: "warnings", hasMore: true)
        viewModel.banPagination.restoreContinuation(endCursor: "bans", hasMore: true)
        viewModel.removalPagination.restoreContinuation(endCursor: "removals", hasMore: true)

        await viewModel.loadMoreNotices(.warnings)
        await viewModel.loadMoreNotices(.bans)
        await viewModel.loadMoreNotices(.removedPosts)

        XCTAssertEqual(viewModel.warningPagination.items.map(\.id), ["warning-2", "warning-revoked"])
        XCTAssertEqual(viewModel.banPagination.items.map(\.id), ["ban-2"])
        XCTAssertEqual(viewModel.removalPagination.items.map(\.postId), ["post-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.compactMap(\.query),
            [
                "limit=25&after=warnings",
                "limit=25&after=bans",
                "limit=25&include_platform=true&after=removals"
            ]
        )
    }

    func testAppealAndNoticePaginationFailuresRemainRetryable() async throws {
        let viewModel = try makeViewModel(route: .tracking)
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: "appeals", hasMore: true)
        viewModel.warningPagination.restoreContinuation(endCursor: "warnings", hasMore: true)
        viewModel.banPagination.restoreContinuation(endCursor: "bans", hasMore: true)
        viewModel.removalPagination.restoreContinuation(endCursor: "removals", hasMore: true)
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.errors["/api/v1/my/warnings"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.errors["/api/v1/my/bans"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.errors["/api/v1/my/removed-posts"] = URLError(.notConnectedToInternet)

        await viewModel.loadMoreAppeals(status: .pending)
        XCTAssertNotNil(viewModel.pendingAppealPagination.lastError)
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)
        await viewModel.loadMoreNotices(.warnings)
        XCTAssertNotNil(viewModel.warningPagination.lastError)
        await viewModel.loadMoreNotices(.bans)
        XCTAssertNotNil(viewModel.banPagination.lastError)
        await viewModel.loadMoreNotices(.removedPosts)
        XCTAssertNotNil(viewModel.removalPagination.lastError)
        XCTAssertNotNil(viewModel.loadMoreErrorMessage)
    }

    func testLoadingFailureSurfacesErrorAndClearsPreviousPages() async throws {
        let viewModel = try makeViewModel(route: .warnings)
        let appeals = try decodedAppeals()
        viewModel.pendingAppealPagination.reset(items: appeals)
        viewModel.suspensionDate = .now
        CannedFeedURLProtocol.errors["/api/v1/appeals"] = URLError(.notConnectedToInternet)
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (emptyPage("warnings"), 200)

        await viewModel.load()

        XCTAssertFalse(viewModel.isLoading)
        XCTAssertTrue(viewModel.pendingAppealPagination.items.isEmpty)
        XCTAssertNil(viewModel.suspensionDate)
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testViewModelStateCoversDraftGuardsMessagesAndPendingTargetKinds() throws {
        let viewModel = try makeViewModel(route: .tracking)
        XCTAssertEqual(viewModel.activeDraft, MemberAppealDraft())
        viewModel.setReason(.other)
        viewModel.setDetails("Ignored")

        viewModel.submissionState = .succeeded(isDuplicate: false)
        XCTAssertNotNil(viewModel.submissionMessage)
        viewModel.submissionState = .succeeded(isDuplicate: true)
        XCTAssertNotNil(viewModel.submissionMessage)
        viewModel.submissionState = .failed(.verbatim("Failure"))
        XCTAssertEqual(viewModel.submissionMessage, .verbatim("Failure"))

        let pending = try decodedAppeals([
            ModerationAppealsTestSupport.appeal(id: "warning"),
            ModerationAppealsTestSupport.appeal(id: "ban")
                .replacingOccurrences(of: #""user_warning_id":"warning-1""#, with: #""user_warning_id":null"#)
                .replacingOccurrences(of: #""community_ban_id":null"#, with: #""community_ban_id":"ban-1""#)
        ])
        viewModel.pendingAppealPagination.reset(items: pending)
        XCTAssertFalse(viewModel.canAppeal(.warning(
            id: "warning-1", message: nil, community: nil, createdAt: .now
        )))
        XCTAssertFalse(viewModel.canAppeal(.ban(
            id: "ban-1", reason: nil, community: nil, createdAt: .now
        )))

        let target = MemberAppealTarget.warning(
            id: "new-warning", message: nil, community: nil, createdAt: .now
        )
        viewModel.beginAppeal(target)
        viewModel.isPresentingTurnstile = true
        viewModel.turnstileToken = "token"
        viewModel.cancelAppeal()
        XCTAssertNil(viewModel.activeTarget)
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertEqual(viewModel.submissionState, .idle)
    }

    private func makeViewModel(route: MemberAppealsRoute) throws -> MemberAppealsViewModel {
        try MemberAppealsViewModel(
            client: makeClient(),
            isSignedIn: true,
            currentUserId: "user-1",
            route: route,
            draftStore: MemberAppealDraftStore()
        )
    }

    private func suspensionTargetId(for viewModel: MemberAppealsViewModel) throws -> String {
        let date = try XCTUnwrap(viewModel.suspensionDate)
        return MemberAppealTarget.suspension(date: date).id
    }

    private func decodedAppeals(
        _ appeals: [String] = [ModerationAppealsTestSupport.appeal()]
    ) throws -> [ModerationAppeal] {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            ModerationAppealListResponse.self,
            from: ModerationAppealsTestSupport.list(appeals)
        ).appeals
    }

    private func emptyPage(_ collection: String) -> Data {
        Data("""
        {"\(collection)":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """.utf8)
    }

    private func warningPage(activeId: String, revokedId: String) -> Data {
        Data("""
        {"warnings":[
          {"id":"\(activeId)","case_id":null,"user_id":"user-1","community_id":null,
           "community_slug":null,"public_message":"Active","revoked_at":null,
           "created_at":"2026-07-01T12:00:00Z"},
          {"id":"\(revokedId)","case_id":null,"user_id":"user-1","community_id":null,
           "community_slug":null,"public_message":"Revoked",
           "revoked_at":"2026-07-02T12:00:00Z",
           "created_at":"2026-07-01T12:00:00Z"}
        ],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """.utf8)
    }

    private func banPage(id: String) -> Data {
        Data("""
        {"bans":[{
          "id":"\(id)","user_id":"user-1","community_id":"community-1",
          "community_slug":"community","reason":"Repeated abuse","expires_at":null,
          "lifted_at":null,"created_at":"2026-07-01T12:00:00Z",
          "updated_at":"2026-07-01T12:00:00Z"
        }],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """.utf8)
    }

    private func removalPage(postId: String) -> Data {
        Data("""
        {"removed_posts":[{
          "__entity_type":"member_removed_post_notice","post_id":"\(postId)",
          "community_id":"community-1","community_slug":"community",
          "post_title":"Removed post","post_removal_kind":"platform",
          "unpublished_at":"2026-07-01T12:00:00Z"
        }],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
        """.utf8)
    }
}
