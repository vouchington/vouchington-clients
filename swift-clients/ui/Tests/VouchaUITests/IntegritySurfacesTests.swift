import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class IntegritySurfacesTests: XCTestCase {
    func testReportCardRendersEvidenceCountsLifecycleAndCanonicalUserDestination() throws {
        let flag = try IntegrityTestSupport.reportFlag(
            postId: nil,
            userId: "user-1",
            resolution: "penalized"
        )
        let viewModel = ReportIntegrityViewModel(service: nil, viewerTier: .administrator)
        viewModel.flags = [flag]
        viewModel.penalizedReporterCounts[flag.id] = 3
        var path: String?
        let inspection = try ReportIntegritySurface(
            viewModel: viewModel,
            onNavigate: { path = $0 }
        ).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Suspected mass report abuse"))
        XCTAssertNoThrow(try inspection.find(text: "report-1"))
        XCTAssertNoThrow(try inspection.find(text: "5"))
        XCTAssertNoThrow(try inspection.find(text: "60%"))
        XCTAssertNoThrow(try inspection.find(text: "Mark penalized"))
        XCTAssertNoThrow(try inspection.find(text: "admin-1"))
        XCTAssertNoThrow(try inspection.find(text: "Penalized reporters: 3"))
        XCTAssertNoThrow(try inspection.find(text: "evidence: captured"))
        XCTAssertNoThrow(try inspection.find(text: "window_minutes: 30"))
        XCTAssertThrowsError(try inspection.find(button: "Dismiss"))

        try inspection.find(button: "User user-1").tap()
        XCTAssertEqual(path, "/user/user-1")
    }

    func testReportPostAndRssTargetsStayIdOnly() throws {
        let post = try IntegrityTestSupport.reportFlag(id: "post-flag")
        let rss = try IntegrityTestSupport.reportFlag(
            id: "rss-flag",
            postId: nil,
            rssFeedItemId: "rss-1"
        )
        let viewModel = ReportIntegrityViewModel(service: nil, viewerTier: .administrator)
        viewModel.flags = [post, rss]
        let inspection = try ReportIntegritySurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Post or comment ID post-1"))
        XCTAssertNoThrow(try inspection.find(text: "RSS item ID rss-1"))
        XCTAssertThrowsError(try inspection.find(button: "Post or comment ID post-1"))
        XCTAssertThrowsError(try inspection.find(button: "RSS item ID rss-1"))
    }

    func testVoteCardRendersActionsEvidenceAndCanonicalTopicDestination() throws {
        let flag = try IntegrityTestSupport.voteFlag(
            postId: nil,
            topicId: "topic-1"
        )
        let viewModel = VoteIntegrityViewModel(service: nil, viewerTier: .administrator)
        viewModel.flags = [flag]
        viewModel.penalizedUserCounts[flag.id] = 4
        var path: String?
        let inspection = try VoteIntegritySurface(
            viewModel: viewModel,
            onNavigate: { path = $0 }
        ).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Vote velocity spike"))
        XCTAssertNoThrow(try inspection.find(text: "vote-1"))
        XCTAssertNoThrow(try inspection.find(text: "Pending"))
        XCTAssertNoThrow(try inspection.find(text: "Penalized users: 4"))
        XCTAssertNoThrow(try inspection.find(text: "evidence: captured"))
        XCTAssertNoThrow(try inspection.find(text: "vote_count: 20"))
        for action in ["Dismiss", "Mark penalized", "Suspend", "Apply vote penalty"] {
            XCTAssertNoThrow(try inspection.find(button: action))
        }

        try inspection.find(button: "Topic topic-1").tap()
        XCTAssertEqual(path, "/topic/topic-1")
    }

    func testVoteDomainIsCanonicalAndUnknownTypesStayIdOnly() throws {
        let domain = try IntegrityTestSupport.voteFlag(
            id: "domain-flag",
            postId: nil,
            hostnameId: "domain-1"
        )
        let relation = try IntegrityTestSupport.voteFlag(
            id: "relation-flag",
            postId: nil,
            entityRelationId: "relation-1"
        )
        let viewModel = VoteIntegrityViewModel(service: nil, viewerTier: .administrator)
        viewModel.flags = [domain, relation]
        var path: String?
        let inspection = try VoteIntegritySurface(
            viewModel: viewModel,
            onNavigate: { path = $0 }
        ).inspect()

        try inspection.find(button: "Domain domain-1").tap()
        XCTAssertEqual(path, "/domain/domain-1")
        XCTAssertNoThrow(try inspection.find(text: "Entity relation ID relation-1"))
        XCTAssertThrowsError(try inspection.find(button: "Entity relation ID relation-1"))
    }

    func testLoadingEmptyInitialAndContinuationErrorsRenderRetryStates() throws {
        let report = ReportIntegrityViewModel(service: nil, viewerTier: .administrator)
        report.initialErrorMessage = "Initial failed."
        var inspection = try ReportIntegritySurface(viewModel: report).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Unable to load integrity records."))
        XCTAssertNoThrow(try inspection.find(button: "Retry"))

        report.initialErrorMessage = nil
        inspection = try ReportIntegritySurface(viewModel: report).inspect()
        XCTAssertNoThrow(try inspection.find(text: "No flags"))

        let vote = VoteIntegrityViewModel(service: nil, viewerTier: .administrator)
        vote.flags = try [IntegrityTestSupport.voteFlag()]
        vote.hasMore = true
        vote.continuationErrorMessage = "More failed."
        let voteInspection = try VoteIntegritySurface(viewModel: vote).inspect()
        XCTAssertNoThrow(try voteInspection.find(text: "Unable to load more records."))
        XCTAssertNoThrow(try voteInspection.find(button: "Retry loading more"))
        XCTAssertNoThrow(try voteInspection.find(button: "Load more"))
    }

    func testRoutesRejectModeratorSupportMemberAndAnonymousViewers() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/report-integrity/flags"))
        let viewers: [(Bool, Bool, Bool, Bool)] = [
            (false, false, false, false),
            (true, false, false, false),
            (true, false, true, false),
            (true, false, false, true)
        ]

        for (isSignedIn, isAdmin, isModerator, isSupport) in viewers {
            let inspection = try NativeRouteDestinationView(
                entry: route.entry,
                routeMatch: route.match,
                isSignedIn: isSignedIn,
                isAdministrator: isAdmin,
                isSiteModerator: isModerator,
                isCustomerSupport: isSupport
            ).inspect()
            let title = isSignedIn ? "Administrator required" : "Sign in required"
            XCTAssertNoThrow(try inspection.find(text: title))
            XCTAssertThrowsError(try inspection.find(button: "Penalize reporters"))
        }
    }

    func testAdministratorRoutesUseDedicatedReportAndVoteSurfaces() throws {
        for path in IntegrityRouteKind.allCases.map(\.rawValue) {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let inspection = try NativeRouteDestinationView(
                entry: route.entry,
                routeMatch: route.match,
                isSignedIn: true,
                isAdministrator: true
            ).inspect()

            let title = path.contains("penalties")
                ? (path.contains("report") ? "Report integrity penalties" : "Vote integrity penalties")
                : (path.contains("report") ? "Report integrity flags" : "Vote integrity flags")
            XCTAssertNoThrow(try inspection.find(text: title))
            XCTAssertNoThrow(try inspection.find(text: path.contains("penalties") ? "Active" : "Pending"))
            XCTAssertNoThrow(try inspection.find(text: path.contains("penalties") ? "Revoked" : "Resolved"))
            XCTAssertNoThrow(try inspection.find(text: "All"))
        }
    }

    func testReportTargetPresentationCoversEverySupportedTarget() throws {
        let targets = try [
            reportIntegrityTarget(IntegrityTestSupport.reportFlag()),
            reportIntegrityTarget(IntegrityTestSupport.reportFlag(postId: nil, userId: "user-1")),
            reportIntegrityTarget(IntegrityTestSupport.reportFlag(postId: nil, hostnameId: "domain-1")),
            reportIntegrityTarget(IntegrityTestSupport.reportFlag(postId: nil, rssFeedItemId: "rss-1")),
            reportIntegrityTarget(IntegrityTestSupport.reportFlag(postId: nil))
        ]

        XCTAssertEqual(targets.map(\.id), ["post-1", "user-1", "domain-1", "rss-1", "report-1"])
        XCTAssertEqual(targets.map(\.path), [
            nil,
            "/user/user-1",
            "/domain/domain-1",
            nil,
            nil
        ])
    }

    func testVoteTargetPresentationCoversEverySupportedTarget() throws {
        let targets = try [
            voteIntegrityTarget(IntegrityTestSupport.voteFlag()),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil, topicId: "topic-1")),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil, hostnameId: "domain-1")),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil, rssFeedItemId: "rss-1")),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil, entityRelationId: "relation-1")),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil, agentModerationId: "agent-1")),
            voteIntegrityTarget(IntegrityTestSupport.voteFlag(postId: nil))
        ]

        XCTAssertEqual(targets.map(\.id), [
            "post-1", "topic-1", "domain-1", "rss-1", "relation-1", "agent-1", "vote-1"
        ])
        XCTAssertEqual(targets.map(\.path), [
            nil,
            "/topic/topic-1",
            "/domain/domain-1",
            nil,
            nil,
            nil,
            nil
        ])
    }

    func testIntegrityDetailsRendersEveryJSONValueInStableOrder() {
        let details: [String: DecodedJSONValue] = [
            "string": .string("captured"),
            "object": .object(["z": .null, "a": .bool(false)]),
            "number": .number(30),
            "null": .null,
            "bool": .bool(true),
            "array": .array([.string("item"), .number(2)])
        ]

        XCTAssertEqual(integrityDetails(details, locale: Locale(identifier: "en")), [
            "array: [item, 2]",
            "bool: true",
            "null: null",
            "number: 30",
            "object: {a: false, z: null}",
            "string: captured"
        ])
    }

    func testIntegrityErrorMessageUsesVouchaAndGenericDescriptions() {
        XCTAssertEqual(
            integrityErrorMessage(VouchaError.forbidden(preconditionCode: nil)),
            "You don't have permission to do this."
        )
        XCTAssertEqual(
            integrityErrorMessage(NSError(domain: "IntegrityTests", code: 7, userInfo: [
                NSLocalizedDescriptionKey: "Request failed."
            ])),
            "Request failed."
        )
    }
}
