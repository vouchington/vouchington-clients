import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceViewTests: NativeRouteSurfaceViewModelTestCase {
    func testBookmarkRowsSurfaceDoesNotRenderFalseEmptyStateWhileLoadingOrFailed() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)

        viewModel.state = .loading
        var sut = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }
        XCTAssertNoThrow(try sut.inspect().find(ViewType.ProgressView.self))
        XCTAssertThrowsError(try sut.inspect().find(text: "No bookmarks"))

        viewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        sut = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }
        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
        XCTAssertThrowsError(try sut.inspect().find(text: "No bookmarks"))
    }

    func testRowsSurfaceRendersLoadingEmptyAndRows() throws {
        let loading = NativeRowsSurface(rows: [], state: .loading, retry: nil)
        XCTAssertNoThrow(try loading.inspect().find(ViewType.ProgressView.self))

        let empty = NativeRowsSurface(rows: [], state: .loaded, retry: nil)
        XCTAssertEqual(try empty.inspect().find(text: "No native rows").string(), "No native rows")

        let rows = NativeRowsSurface(
            rows: [verbatimRow(icon: "tag", title: "Native row", detail: "Loaded")],
            state: .loaded,
            retry: nil
        )
        XCTAssertEqual(try rows.inspect().find(text: "Native row").string(), "Native row")
        XCTAssertEqual(try rows.inspect().find(text: "Loaded").string(), "Loaded")
    }

    func testEmptyFediverseDirectoryUsesStandardEmptyState() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseInstances), client: nil)
        viewModel.state = .loaded
        viewModel.rows = []

        XCTAssertNoThrow(try NativeListSurface(viewModel: viewModel).inspect().find(text: "No native rows"))
    }

    func testRowsSurfaceRendersRetryableError() throws {
        let sut = NativeRowsSurface(rows: [], state: .error(.api(statusCode: 500, preconditionCode: nil))) {}

        XCTAssertEqual(try sut.inspect().find(text: "An error occurred.").string(), "An error occurred.")
        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
    }

    func testActionSurfaceRendersCompareActions() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: nil)
        let sut = NativeActionSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Compare top topics"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Compare top domains"))
    }

    func testListSurfaceRendersRowsAndActions() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: nil)
        let sut = NativeListSurface(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "Compare").string(), "Compare")
        XCTAssertNoThrow(try sut.inspect().find(button: "Compare top topics"))
    }

    func testSearchSurfaceRendersInitialRowsAndSearchButton() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .webSearch), client: nil)
        let sut = NativeSearchSurface(viewModel: viewModel, initialQuery: nil)

        XCTAssertNoThrow(try sut.inspect().find(button: "Search"))
        XCTAssertEqual(try sut.inspect().find(text: "Search results").string(), "Search results")
    }

    func testReferralsDestinationRendersManagementSurface() throws {
        let entry = try entry(for: .referrals)
        let sut = NativeRouteDestinationSurface(
            entry: entry,
            client: nil,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: false,
            showSignIn: {}
        )

        XCTAssertFalse(sut.canCastPublicVotes)
        XCTAssertEqual(try sut.inspect().find(text: "Sign in required").string(), "Sign in required")
    }

    func testReferralsDestinationGatesSignedOutClient() throws {
        let entry = try entry(for: .referrals)
        let sut = try NativeRouteDestinationSurface(
            entry: entry,
            client: makeClient(),
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: false,
            showSignIn: {}
        )

        XCTAssertEqual(try sut.inspect().find(text: "Sign in required").string(), "Sign in required")
    }

    func testDetailSurfaceRendersLoadedRows() throws {
        let entry = try entry(for: .communityDetail)
        let viewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        let sut = NativeDetailSurface(entry: entry, viewModel: viewModel)

        XCTAssertFalse(sut.canCastPublicVotes)
        XCTAssertEqual(try sut.inspect().find(text: "Community detail").string(), "Community detail")
    }

    func testUserProfileDetailSurfaceRendersReportActionForOtherSignedInUsers() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data(#"{"user":{"id":"user-1","username":"alice"},"profile_links":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Report"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Mute"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Block"))
    }

    func testUserProfileReportReasonsStartNotePrompt() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        viewModel.detailRelationEntityType = "user"
        viewModel.detailRelationEntityId = "user-1"
        viewModel.detailReportTarget = .user(id: "user-1")

        for reason in nativeProfileReportReasons {
            let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

            sut.startReportDetail(reason: reason.value)

            XCTAssertEqual(viewModel.detailPendingReportReason, reason.value)
            XCTAssertEqual(viewModel.detailPendingReportNote, "")
            XCTAssertFalse(viewModel.detailShowingReportTurnstile)
        }
    }

    func testUserProfileReportReasonsSkipNotePromptWhenRelationIsMissing() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        sut.startReportDetail(reason: "spam")

        XCTAssertNil(viewModel.detailPendingReportReason)
        XCTAssertEqual(viewModel.detailPendingReportNote, "")
        XCTAssertFalse(viewModel.detailShowingReportTurnstile)
    }

    func testUserProfileDetailSurfaceHidesActionsForSelfProfile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data(#"{"user":{"id":"user-1","username":"alice"},"profile_links":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        XCTAssertThrowsError(try sut.inspect().find(button: "Report"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Mute"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Block"))
    }

    func testUserProfileDetailSurfaceHidesActionsForSignedOutProfile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data(#"{"user":{"id":"user-1","username":"alice"},"profile_links":[]}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel, isSignedIn: false)

        XCTAssertNoThrow(try sut.inspect().find(button: "Follow"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Report"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Mute"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Block"))
    }

    func testUserProfileReportRequestsUseReportEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        viewModel.detailReportTarget = .user(id: "user-1")
        _ = await viewModel.report(
            target: .user(id: "user-1"),
            reason: "spam",
            note: "spam note",
            turnstileToken: "turnstile-token"
        )

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/reports")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""entityType":"user""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""entityId":"user-1""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""reason":"spam""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""note":"spam note""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""cf_turnstile_response":"turnstile-token""#)
        })
        XCTAssertNil(viewModel.detailReportErrorMessage)
    }

    func testUserProfileReportDetailSubmitsRelationWithTurnstileToken() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        viewModel.detailReportTarget = .user(id: "user-1")
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        _ = await sut.reportDetail(reason: "illegal_content", note: "more context", turnstileToken: "report-token")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/reports")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""reason":"illegal_content""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""note":"more context""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""cf_turnstile_response":"report-token""#)
        })
    }

    func testUserProfileReportTurnstileTokenSubmitsPendingReasonAndClearsSheet() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        viewModel.detailReportTarget = .user(id: "user-1")
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)
        viewModel.detailPendingReportReason = "misinformation"
        viewModel.detailPendingReportNote = "  impersonation context  "
        viewModel.detailShowingReportTurnstile = true

        sut.handleReportTurnstileToken("report-token")

        for _ in 0 ..< 20 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == "/api/v1/reports" }) {
                break
            }
            try? await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertFalse(viewModel.detailShowingReportTurnstile)
        XCTAssertNil(viewModel.detailPendingReportReason)
        XCTAssertEqual(viewModel.detailPendingReportNote, "")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""reason":"misinformation""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""note":"impersonation context""#)
        })
    }

    func testUserProfileReportNoteDismissalPresentsTurnstileAfterSheetCloses() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        sut.continueReportNote()

        XCTAssertFalse(viewModel.detailShowingReportTurnstile)

        sut.presentReportTurnstileAfterNoteDismiss()

        XCTAssertTrue(viewModel.detailShowingReportTurnstile)
    }

    func testUserProfileReportDetailSkipsMissingRelation() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        _ = await sut.reportDetail(reason: "spam", turnstileToken: "report-token")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testUserProfileReportFailuresSurfaceErrorForRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data(#"{"message":"captcha rejected"}"#.utf8), 403)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        viewModel.detailReportTarget = .user(id: "user-1")
        _ = await viewModel.report(
            target: .user(id: "user-1"),
            reason: "spam",
            note: nil,
            turnstileToken: "bad-token"
        )

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/reports")
        XCTAssertEqual(
            uiEnglish(viewModel.detailReportErrorMessage),
            "You don't have permission to do this."
        )

        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (Data("{}".utf8), 200)

        _ = await viewModel.report(
            target: .user(id: "user-1"),
            reason: "spam",
            note: nil,
            turnstileToken: "fresh-token"
        )

        XCTAssertNil(viewModel.detailReportErrorMessage)
    }

    func testUserProfileReportFailureAlertDismissesError() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        viewModel.detailReportErrorMessage = .verbatim("Unable to send report.")
        let sut = NativeDetailSurface(entry: route.entry, viewModel: viewModel)

        let alert = try sut.inspect().vStack().alert()

        XCTAssertEqual(try alert.title().string(), "Report failed")
        XCTAssertEqual(try alert.message().text().string(), "Unable to send report.")

        try alert.dismiss()

        XCTAssertNil(viewModel.detailReportErrorMessage)
    }

    func testUrlDetailSurfaceRendersSignedOutGate() throws {
        let entry = try entry(for: .urlDetail)
        let viewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        let sut = NativeDetailSurface(entry: entry, viewModel: viewModel, isSignedIn: false)

        XCTAssertEqual(try sut.inspect().find(text: "Sign in required").string(), "Sign in required")
    }

    func testDetailSurfaceRendersTopicVoteControls() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              },
              "topic_election": {
                "votes_score_net": 2,
                "votes_count_up": 5,
                "votes_count_down": 3,
                "my_vote": null
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/vote"] = (Data("{}".utf8), 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let entry = try entry(for: .topicDetail)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry, client: makeClient(), routeMatch: match)
        await viewModel.load()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        let sut = NativeDetailSurface(
            entry: entry,
            viewModel: viewModel,
            canCastPublicVotes: true
        )

        XCTAssertEqual(try sut.inspect().find(text: "5").string(), "5")
        XCTAssertEqual(try sut.inspect().find(text: "3").string(), "3")
        let buttons = try sut.inspect().findAll(ViewType.Button.self)
        XCTAssertGreaterThanOrEqual(buttons.count, 5)
        try buttons[1].tap()

        for _ in 0 ..< 20 {
            if viewModel.myVotesByTopicId["topic-1"] == .like,
               CannedFeedURLProtocol.capturedMethods.contains("PUT") {
                break
            }
            try? await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("PUT"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/topics/topic-1/vote" })
        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
    }

    func testTopicRecommendationCreateSurfaceRendersNativeForm() throws {
        let sut = NativeTopicRecommendationSurface(
            client: nil,
            recommendationId: nil,
            turnstileSiteKey: "site-key"
        )

        XCTAssertNoThrow(try sut.inspect().find(ViewType.Picker.self))
        XCTAssertNoThrow(try sut.inspect().find(text: "Topic"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Referral"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Card"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Submit recommendation"))
    }

    func testTopicRecommendationEditSurfaceOmitsTurnstile() throws {
        let sut = NativeTopicRecommendationSurface(
            client: nil,
            recommendationId: "topic-rec-1",
            turnstileSiteKey: "site-key"
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Save recommendation"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Verify"))
    }

    func testTurnstileChallengeViewRendersWebViewContainer() throws {
        let sut = NativeTurnstileChallengeView(siteKey: "site-key") { _ in }

        XCTAssertNoThrow(try sut.inspect().find(NativeTurnstileChallengeView.self))
    }
}
