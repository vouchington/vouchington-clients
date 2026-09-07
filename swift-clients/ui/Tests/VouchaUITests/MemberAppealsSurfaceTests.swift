import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testMemberSurfaceRendersEligibleNoticeAndTrackingStates() throws {
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        viewModel.suspensionDate = .now
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: viewModel)
        let inspection = try MemberAppealsSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Suspension appeal"))
        XCTAssertNoThrow(try inspection.find(button: "File appeal"))
    }

    func testSignedOutSurfaceRendersSignInState() throws {
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: false,
            currentUserId: nil,
            route: .tracking,
            draftStore: MemberAppealDraftStore()
        )
        XCTAssertNoThrow(try MemberAppealsSurface(viewModel: viewModel).inspect().find(text: "Sign in required"))
    }

    func testMemberRoutesUseDedicatedSurfaceIncludingSuspensionNotice() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/account-status"))
        let surface = NativeRouteDestinationSurface(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            showSignIn: {}
        )
        XCTAssertTrue(surface.isDedicatedMemberAppealsRoute)
        XCTAssertFalse(surface.shouldLoadRouteSurfaceContent)
    }

    func testMemberAppealsStateIdentityIncludesRouteAuthenticationAndAccount() throws {
        let warningsRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/warnings"))
        let warnings = NativeRouteDestinationSurface(
            entry: warningsRoute.entry,
            client: nil,
            routeMatch: warningsRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            showSignIn: {}
        )
        let identity = warnings.memberAppealsIdentity(route: .warnings)

        XCTAssertNotEqual(identity, warnings.memberAppealsIdentity(route: .bans))
        XCTAssertNotEqual(
            identity,
            MemberAppealsSurfaceIdentity.state(
                route: .warnings,
                isSignedIn: false,
                currentUserId: "user-1"
            )
        )
        XCTAssertNotEqual(
            identity,
            MemberAppealsSurfaceIdentity.state(
                route: .warnings,
                isSignedIn: true,
                currentUserId: "user-2"
            )
        )
    }

    func testRemovalTargetsKeepPlatformAndCommunityDecisionsDistinct() {
        let platform = MemberAppealTarget.removal(
            id: "post-1",
            title: "Post",
            community: nil,
            kind: .platform,
            date: .now
        )
        let community = MemberAppealTarget.removal(
            id: "post-1",
            title: "Post",
            community: "community",
            kind: .community,
            date: .now
        )

        XCTAssertEqual(platform.id, "removal:platform:post-1")
        XCTAssertEqual(community.id, "removal:community:post-1")
        XCTAssertEqual(platform.targetId, community.targetId)
    }

    func testAppealFormOwnsReachableTurnstilePresentation() throws {
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: viewModel)
        viewModel.beginAppeal(.warning(id: "warning-1", message: nil, community: nil, createdAt: .now))
        let inspection = try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect()

        try inspection.find(button: "Verify").tap()
        XCTAssertTrue(viewModel.isPresentingTurnstile)
        XCTAssertNoThrow(try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect().find(NativeTurnstileChallengeView.self))
    }

    func testAppealFormBindingsMessagesAndCancellationStayConnectedToDraftState() throws {
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: viewModel)
        viewModel.beginAppeal(.warning(
            id: "warning-1",
            message: nil,
            community: nil,
            createdAt: .now
        ))
        let form = MemberAppealForm(viewModel: viewModel, turnstileSiteKey: "site-key")
        let inspection = try form.inspect()

        try inspection.find(ViewType.Picker.self).select(value: Optional(ModerationAppealReason.wrongRule))
        try inspection.find(ViewType.TextEditor.self).setInput("The cited rule does not apply.")
        XCTAssertEqual(viewModel.activeDraft.reason, .wrongRule)
        XCTAssertEqual(viewModel.activeDraft.details, "The cited rule does not apply.")
        XCTAssertTrue(try inspection.find(button: "Submit appeal").isDisabled())

        viewModel.turnstileToken = "verified"
        let verified = try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect()
        XCTAssertFalse(try verified.find(button: "Submit appeal").isDisabled())
        XCTAssertNoThrow(try verified.find(button: "Verified"))

        viewModel.submissionState = .succeeded(isDuplicate: false)
        XCTAssertNoThrow(try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect().find(text: "Appeal submitted"))

        viewModel.submissionState = .failed(.userContent("Server rejected the appeal"))
        XCTAssertNoThrow(try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect().find(text: "Server rejected the appeal"))

        try MemberAppealForm(
            viewModel: viewModel,
            turnstileSiteKey: "site-key"
        ).inspect().find(button: "Cancel").tap()
        XCTAssertNil(viewModel.activeTarget)
        XCTAssertNil(viewModel.turnstileToken)
    }

    func testNoticeCardsRenderEveryTargetKindAndRespectEligibility() throws {
        let date = Date(timeIntervalSince1970: 1_719_828_000)
        let cases: [(MemberAppealTarget, String, String?)] = [
            (
                .warning(id: "warning-1", message: "Warning context", community: "fallback", createdAt: date),
                "Warning appeal",
                "Warning context"
            ),
            (
                .ban(id: "ban-1", reason: "Ban context", community: "fallback", createdAt: date),
                "Community ban appeal",
                "Ban context"
            ),
            (
                .removal(
                    id: "post-1",
                    title: "Removed post",
                    community: "fallback",
                    kind: .platform,
                    date: date
                ),
                "Platform post removal appeal",
                "Removed post"
            ),
            (.suspension(date: date), "Suspension appeal", nil)
        ]
        var appealedIds: [String] = []

        for (target, title, context) in cases {
            let inspection = try MemberAppealNoticeCard(
                target: target,
                canAppeal: true,
                onAppeal: { appealedIds.append(target.id) }
            ).inspect()
            XCTAssertNoThrow(try inspection.find(text: title))
            if let context {
                XCTAssertNoThrow(try inspection.find(text: context))
            }
            try inspection.find(button: "File appeal").tap()
        }

        XCTAssertEqual(appealedIds, cases.map(\.0.id))
        let pending = try MemberAppealNoticeCard(
            target: cases[0].0,
            canAppeal: false,
            onAppeal: {}
        ).inspect().find(button: "Appeal pending")
        XCTAssertTrue(pending.isDisabled())
    }

    func testNoticePaginationItemReflectsLoadingContinuationAndFailure() throws {
        var pagination = CursorPaginationState<MemberWarningNotice>()
        let initial = MemberAppealNoticePaginationItem(kind: .warnings, pagination: pagination)
        XCTAssertEqual(initial.id, "warnings")
        XCTAssertFalse(initial.isRelevant)

        let request = try XCTUnwrap(pagination.beginNextPage())
        let loading = MemberAppealNoticePaginationItem(kind: .bans, pagination: pagination)
        XCTAssertEqual(loading.id, "bans")
        XCTAssertTrue(loading.isLoading)

        XCTAssertTrue(pagination.complete(
            request,
            items: [],
            endCursor: "next",
            hasNextPage: true
        ))
        let continuation = MemberAppealNoticePaginationItem(
            kind: .removedPosts,
            pagination: pagination
        )
        XCTAssertEqual(continuation.id, "removed-posts")
        XCTAssertTrue(continuation.hasMore)
        XCTAssertTrue(continuation.isRelevant)

        let failedRequest = try XCTUnwrap(pagination.beginNextPage())
        XCTAssertTrue(pagination.fail(
            failedRequest,
            error: .api(statusCode: 503, preconditionCode: nil)
        ))
        let failed = MemberAppealNoticePaginationItem(kind: .warnings, pagination: pagination)
        XCTAssertTrue(failed.hasError)
        XCTAssertTrue(failed.isRelevant)
    }

    func testNoticePaginationRemainsReachableWhenCurrentPageHasNoEligibleTargets() throws {
        let revokedWarning = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberWarningNotice.self,
            from: Data("""
            {
              "id": "warning-revoked",
              "case_id": null,
              "user_id": "user-1",
              "community_id": null,
              "community_slug": null,
              "public_message": "Revoked warning",
              "revoked_at": "2026-07-01T10:00:00Z",
              "created_at": "2026-07-01T09:00:00Z"
            }
            """.utf8)
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .warnings,
            draftStore: MemberAppealDraftStore()
        )
        viewModel.warningPagination.reset(items: [revokedWarning])
        viewModel.warningPagination.restoreContinuation(endCursor: "older", hasMore: true)

        XCTAssertTrue(viewModel.eligibleTargets.isEmpty)
        XCTAssertNoThrow(try MemberAppealsSurface(viewModel: viewModel).inspect().find(button: "Load more"))
    }

    func testTrackingCardRendersMemberLifecycleWithoutStaffContextOrControls() throws {
        let appeal = try fixtureAppeal(
            status: "dismissed",
            approvedAt: "2026-07-01T11:00:00Z",
            sentAt: "2026-07-01T12:00:00Z",
            resolvedAt: "2026-07-01T13:00:00Z",
            resolutionAction: "deny",
            internalNotes: "Staff-only history"
        )
        let inspection = try MemberAppealTrackingCard(appeal: appeal).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Approved"))
        XCTAssertNoThrow(try inspection.find(text: "Sent"))
        XCTAssertNoThrow(try inspection.find(text: "Dismissed"))
        XCTAssertThrowsError(try inspection.find(text: "Staff-only history"))
        XCTAssertThrowsError(try inspection.find(button: "Approve"))
    }

    func testTrackingCardsRenderEveryTargetStatusAndResolutionAction() throws {
        let warning = try fixtureAppeal(
            status: "pending",
            approvedAt: nil,
            sentAt: nil,
            resolvedAt: nil,
            resolutionAction: nil,
            internalNotes: nil
        )
        let ban = try fixtureAppeal(
            status: "resolved",
            approvedAt: nil,
            sentAt: nil,
            resolvedAt: "2026-07-01T13:00:00Z",
            resolutionAction: "accept",
            internalNotes: nil,
            replacing: [
                (#""user_warning_id":"warning-1""#, #""user_warning_id":null"#),
                (#""community_ban_id":null"#, #""community_ban_id":"ban-1""#)
            ]
        )
        let removal = try fixtureAppeal(
            status: "resolved",
            approvedAt: nil,
            sentAt: nil,
            resolvedAt: "2026-07-01T13:00:00Z",
            resolutionAction: "reduce",
            internalNotes: nil,
            postId: "post-1"
        )
        let suspension = try fixtureAppeal(
            status: "dismissed",
            approvedAt: nil,
            sentAt: nil,
            resolvedAt: "2026-07-01T13:00:00Z",
            resolutionAction: "deny",
            internalNotes: nil,
            suspensionId: "suspension-1"
        )

        for (appeal, title, status, action) in [
            (warning, "Warning appeal", "Pending", nil),
            (ban, "Community ban appeal", "Resolved", "Accept"),
            (removal, "Post removal appeal", "Resolved", "Reduce"),
            (suspension, "Suspension appeal", "Dismissed", "Deny")
        ] {
            let inspection = try MemberAppealTrackingCard(appeal: appeal).inspect()
            XCTAssertNoThrow(try inspection.find(text: title))
            XCTAssertNoThrow(try inspection.find(text: status))
            if let action {
                XCTAssertNoThrow(try inspection.find(text: action))
            }
        }
    }

    func testTrackingCardsPreferMemberSafeTargetContextOverGenericTitles() throws {
        let cases: [(String, String, String)] = [
            (
                """
                {"type":"warning","id":"warning-1","public_message":"The warning message",\
                "community":{"id":"community-1","name":"Builders"},"created_at":"2026-07-01T09:00:00Z"}
                """,
                "The warning message",
                "Warning appeal"
            ),
            (
                """
                {"type":"community_ban","id":"ban-1","community":{"id":"community-1","name":"Builders"},\
                "reason":"The ban reason","expires_at":null,"created_at":"2026-07-01T09:00:00Z"}
                """,
                "The ban reason",
                "Community ban appeal"
            ),
            (
                """
                {"type":"post_removal","id":"post-1","title":"A removed review","declared_language":null,"lingua_rs_detected_language":null,"kind":"platform",\
                "community":null,"public_reason":"Policy reason","decided_at":"2026-07-01T09:00:00Z"}
                """,
                "A removed review",
                "Post removal appeal"
            ),
            (
                """
                {"type":"suspension","id":"suspension-1","reason":"The suspension reason",\
                "created_at":"2026-07-01T09:00:00Z"}
                """,
                "The suspension reason",
                "Suspension appeal"
            )
        ]

        for (targetContext, expectedTitle, genericTitle) in cases {
            let appeal = try fixtureAppeal(
                status: "pending",
                approvedAt: nil,
                sentAt: nil,
                resolvedAt: nil,
                resolutionAction: nil,
                internalNotes: nil,
                replacing: [(
                    #""is_overdue":true"#,
                    #""is_overdue":true,"target_context":\#(targetContext)"#
                )]
            )
            let inspection = try MemberAppealTrackingCard(appeal: appeal).inspect()

            XCTAssertNoThrow(try inspection.find(text: expectedTitle))
            XCTAssertThrowsError(try inspection.find(text: genericTitle))
        }
    }

    func testTrackingCardUsesWarningCommunityWhenMessageIsUnavailable() throws {
        let targetContext = """
        {"type":"warning","id":"warning-1","public_message":"  ",\
        "community":{"id":"community-1","name":"Builders"},"created_at":"2026-07-01T09:00:00Z"}
        """
        let appeal = try fixtureAppeal(
            status: "pending",
            approvedAt: nil,
            sentAt: nil,
            resolvedAt: nil,
            resolutionAction: nil,
            internalNotes: nil,
            replacing: [(
                #""is_overdue":true"#,
                #""is_overdue":true,"target_context":\#(targetContext)"#
            )]
        )
        let inspection = try MemberAppealTrackingCard(appeal: appeal).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Builders"))
        XCTAssertThrowsError(try inspection.find(text: "Warning appeal"))
    }

    private func fixtureAppeal(
        status: String,
        approvedAt: String?,
        sentAt: String?,
        resolvedAt: String?,
        resolutionAction: String?,
        internalNotes: String?,
        postId: String? = nil,
        suspensionId: String? = nil,
        replacing replacements: [(String, String)] = []
    ) throws -> ModerationAppeal {
        var json = ModerationAppealsTestSupport.appeal(
            status: status,
            suspensionId: suspensionId,
            postId: postId,
            publicResponse: "Member-visible response",
            internalNotes: internalNotes,
            approvedAt: approvedAt,
            sentAt: sentAt,
            resolvedAt: resolvedAt,
            resolutionAction: resolutionAction
        )
        for replacement in replacements {
            json = json.replacingOccurrences(of: replacement.0, with: replacement.1)
        }
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationAppeal.self,
            from: Data(json.utf8)
        )
    }
}
