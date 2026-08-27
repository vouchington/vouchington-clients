import ViewInspector
@testable import VouchaFeatures
@testable import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeEngineeringAgentsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testSignedOutAndNonAdministratorGatesRenderBeforeAgentRequests() throws {
        let signedOut = try makeSurface(isSignedIn: false, isAdministrator: false)
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)

        let member = try makeSurface(isSignedIn: true, isAdministrator: false)
        XCTAssertNoThrow(try member.inspect().find(text: "Administrator required"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testDirectoryRowsUseExactSegmentSafeAgentPath() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentDirectoryResults = try [decode(
            AgentSummary.self,
            #"{"id":"agent/a:% 😀","system_user_id":"system","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null}"#
        )]
        XCTAssertEqual(viewModel.agentDirectoryRows().first?.targetPath, "/agent/agent%2Fa%3A%25%20%F0%9F%98%80")
    }

    func testDirectoryRowsUseMergedSystemUsersAndConversationDetailsIncludeLocalizedDates() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentDirectoryResults = try [
            decode(
                AgentSummary.self,
                #"{"id":"first","system_user_id":"user-first","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null}"#
            ),
            decode(
                AgentSummary.self,
                #"{"id":"missing","system_user_id":"user-missing","agent_type":"reviewer","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null}"#
            )
        ]
        viewModel.agentDirectoryUsers = try decode(
            [String: PublicUser].self,
            #"{"user-first":{"id":"user-first","username":"first-user","display_account":{"id":"account","name":"First account"}}}"#
        )
        viewModel.agentConversationListResults = try [decode(
            AgentConversationSummary.self,
            #"{"id":"one","title":"Duplicate","created_at":"2026-07-01T10:00:00Z","created_by_id":"user-first","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        )]
        viewModel.agentConversationListUsers = viewModel.agentDirectoryUsers

        XCTAssertEqual(viewModel.agentDirectoryRows().map(\.title), ["First account", "user-mis"])
        let locale = Locale(identifier: "en_US")
        let directoryCreatedAt = try XCTUnwrap(viewModel.agentDirectoryResults.first?.createdAt)
        let expectedDate = UiMessages.date(
            directoryCreatedAt,
            date: .abbreviated,
            time: .omitted,
            locale: locale,
            timeZone: .gmt
        )
        let expectedInactiveStatus = UiMessages.string(
            UiMessage(.nativeSwiftRouteSurfaceAgentStatusInactive),
            locale: locale,
            timeZone: .gmt
        )
        let directoryDetails = viewModel.agentDirectoryRows().map { $0.localizedDetail(locale: locale, timeZone: .gmt) }
        XCTAssertEqual(directoryDetails.count, 2)
        for (detail, agentType) in zip(directoryDetails, ["helper", "reviewer"]) {
            XCTAssertTrue(detail.contains(agentType))
            XCTAssertTrue(detail.contains(expectedInactiveStatus))
            XCTAssertTrue(detail.contains(expectedDate))
        }
        let detail = try XCTUnwrap(viewModel.agentConversationListRows().first?.localizedDetail(
            locale: locale, timeZone: .gmt
        ))
        XCTAssertTrue(detail.contains("First account"))
        let conversationCreatedAt = try XCTUnwrap(viewModel.agentConversationListResults.first?.createdAt)
        XCTAssertTrue(detail.contains(UiMessages.date(
            conversationCreatedAt,
            date: .abbreviated,
            time: .omitted,
            locale: locale,
            timeZone: .gmt
        )))
        XCTAssertNotEqual(
            detail,
            viewModel.agentConversationListRows().first?.localizedDetail(
                locale: Locale(identifier: "es_ES"),
                timeZone: .gmt
            )
        )
    }

    func testDirectoryRendersLoadingErrorAndActivatesRow() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.state = .loading
        var navigatedPath: String?
        var surface = NativeEngineeringAgentsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            isAdministrator: true,
            onNavigate: { navigatedPath = $0 },
            showSignIn: {}
        )
        XCTAssertNoThrow(try surface.inspect().find(ViewType.ProgressView.self))

        viewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        surface = NativeEngineeringAgentsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            isAdministrator: true,
            onNavigate: { navigatedPath = $0 },
            showSignIn: {}
        )
        XCTAssertNoThrow(try surface.inspect().find(button: "Try Again"))

        viewModel.state = .loaded
        viewModel.agentDirectoryResults = try [decode(
            AgentSummary.self,
            #"{"id":"agent/a","system_user_id":"system","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null}"#
        )]
        viewModel.agentDirectoryUsers = try decode(
            [String: PublicUser].self,
            #"{"system":{"id":"system","username":"system-user","display_account":{"id":"account","name":"System account"}}}"#
        )
        surface = NativeEngineeringAgentsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            isAdministrator: true,
            onNavigate: { navigatedPath = $0 },
            showSignIn: {}
        )
        try surface.inspect().find(button: "System account").tap()
        XCTAssertEqual(navigatedPath, "/agent/agent%2Fa")
    }

    func testInitialDirectoryErrorShowsRetryDespiteMetadataRows() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agents"))
        CannedFeedURLProtocol.handlers["/api/v1/agents"] = (Data("{}".utf8), 500)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case let .error(.api(statusCode, preconditionCode)) = viewModel.state else {
            return XCTFail("Expected the initial agent directory load to fail")
        }
        XCTAssertEqual(statusCode, 500)
        XCTAssertNil(preconditionCode)
        XCTAssertFalse(viewModel.rows.isEmpty)
        let surface = NativeEngineeringAgentsSurface(
            viewModel: viewModel,
            isSignedIn: true,
            isAdministrator: true,
            onNavigate: { _ in },
            showSignIn: {}
        )
        XCTAssertNoThrow(try surface.inspect().find(button: "Try Again"))
    }

    func testEngineeringAgentsRouteUsesScrollableSurfaceContainer() throws {
        let destination = try repoSource("NativeRouteDestinationView.swift")
        let content = try repoSource("NativeRouteDestinationView+DestinationContent.swift")

        XCTAssertFalse(destination.contains("else if entry.destinationIdentifier == .engineeringAgents"))
        XCTAssertTrue(content.contains("case .engineeringAgents:\n                NativeEngineeringAgentsSurface("))
    }

    func testEngineeringAgentRoutesUseBoundHistoryForDetailAndConversation() throws {
        let navigation = try repoSource("swift-clients/apps/shared-app/SectionDetailView+Navigation.swift")
        let detailPath = NativeAgentPath.detail("agent/a").value
        let conversationPath = NativeAgentPath.conversation(
            agentIdOrSlug: "agent/a",
            conversationId: "conversation:b"
        ).value

        XCTAssertEqual(detailPath, "/agent/agent%2Fa")
        XCTAssertEqual(conversationPath, "/agent/agent%2Fa/conversation/conversation%3Ab")
        XCTAssertEqual(
            NativeRouteCatalog.matchingRoute(for: "/agents")?.entry.destinationIdentifier,
            .engineeringAgents
        )
        XCTAssertEqual(
            NativeRouteCatalog.matchingRoute(for: detailPath)?.entry.destinationIdentifier,
            .engineeringAgents
        )
        XCTAssertEqual(
            NativeRouteCatalog.matchingRoute(for: conversationPath)?.entry.destinationIdentifier,
            .engineeringAgents
        )
        XCTAssertTrue(navigation.contains("NavigationStack(path: $nativeRouteHistory)"))
        XCTAssertTrue(navigation.contains("nativeRouteHistory.append(targetPath)"))
        XCTAssertTrue(navigation.contains(".navigationDestination(for: String.self)"))
        XCTAssertTrue(navigation.contains("target.entry.destinationIdentifier == .engineeringAgents"))
        XCTAssertTrue(navigation.contains("nativeRouteHistory = []"))
        XCTAssertTrue(navigation.contains(".onChange(of: activeNativeRouteIdentity)"))
    }

    func testDetailFilterAndTranscriptPresentationBranches() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.state = .loaded
        viewModel.agentDetail = try decode(
            AgentDetailResponse.self,
            #"{"agent":{"id":"agent","system_user_id":"system","agent_type":"helper","activated_at":"2026-07-01T10:00:00Z","deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":"helper","moderator":null},"user":null}"#
        )
        let detail = try makeSurface(viewModel: viewModel, isSignedIn: true, isAdministrator: true)
        XCTAssertNoThrow(try detail.inspect().find(button: "Search"))
        XCTAssertNoThrow(try detail.inspect().find(button: "Clear"))
        XCTAssertNoThrow(try detail.inspect().find(text: "System user ID: system"))
        XCTAssertEqual(
            try detail.inspect().find(ViewType.Picker.self).accessibilityLabel().string(),
            "Conversation filter"
        )
        XCTAssertEqual(
            try detail.inspect().find(ViewType.TextField.self).accessibilityLabel().string(),
            "Username value"
        )
        XCTAssertEqual(
            NativeEngineeringAgentDetailSurface.filterValueAccessibilityLabel(
                .postSlug,
                locale: Locale(identifier: "en")
            ),
            "Post slug value"
        )
    }

    func testWhitespaceAgentMessageContentUsesNonblankErrorOrLocalizedFallback() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversation = try decode(AgentConversation.self, conversationData)
        viewModel.agentConversationMessages = try decode(
            [AgentConversationMessage].self,
            #"[{"id":"whitespace-content","conversation_id":"conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":" \n\t ","error":" Fallback error "}},{"id":"whitespace-content-and-error","conversation_id":"conversation","created_at":"2026-07-01T10:01:00Z","created_by_id":"user","updated_at":"2026-07-01T10:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":" \n\t ","error":" \n\t "}},{"id":"nonblank-content","conversation_id":"conversation","created_at":"2026-07-01T10:02:00Z","created_by_id":"user","updated_at":"2026-07-01T10:02:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"  Original content  ","error":"Ignored error"}}]"#
        )

        let rows = viewModel.agentConversationRows(fallbackId: "conversation", agentSystemUserId: "agent")
        let locale = Locale(identifier: "en_US")

        XCTAssertTrue(rows[1].localizedDetail(locale: locale, timeZone: .gmt).contains(" Fallback error "))
        XCTAssertTrue(rows[2].localizedDetail(locale: locale, timeZone: .gmt).contains("No results"))
        XCTAssertTrue(rows[3].localizedDetail(locale: locale, timeZone: .gmt).contains("  Original content  "))
    }

    func testConversationRowsResolveCreatorNamesAndTranscriptSpeakersByAuthorId() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversationListResults = try [
            decode(
                AgentConversationSummary.self,
                #"{"id":"first","title":"First","created_at":"2026-07-01T10:00:00Z","created_by_id":"support-one","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
            ),
            decode(
                AgentConversationSummary.self,
                #"{"id":"second","title":"Second","created_at":"2026-07-01T10:00:00Z","created_by_id":"missing-user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
            )
        ]
        viewModel.agentConversationListUsers = try decode(
            [String: PublicUser].self,
            #"{"support-one":{"id":"support-one","username":"support-one","display_account":{"id":"account","name":"Support One"}}}"#
        )
        viewModel.agentConversation = try decode(AgentConversation.self, conversationData)
        viewModel.agentConversationMessages = try decode(
            [AgentConversationMessage].self,
            #"[{"id":"selected","conversation_id":"conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"selected-agent","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"Selected agent"}},{"id":"support","conversation_id":"conversation","created_at":"2026-07-01T10:01:00Z","created_by_id":"support-one","updated_at":"2026-07-01T10:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"Support user"}},{"id":"other-agent","conversation_id":"conversation","created_at":"2026-07-01T10:02:00Z","created_by_id":"other-agent","updated_at":"2026-07-01T10:02:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"Other agent"}}]"#
        )

        let details = viewModel.agentConversationListRows().map { $0.localizedDetail(
            locale: Locale(identifier: "en_US"),
            timeZone: .gmt
        ) }
        XCTAssertTrue(details[0].contains("Support One"))
        XCTAssertTrue(details[1].contains("missing-"))
        XCTAssertEqual(
            viewModel.agentConversationRows(fallbackId: "conversation", agentSystemUserId: "selected-agent")
                .map(\.title),
            ["Conversation", "Agent", "User", "User"]
        )
    }

    func testTranscriptRowsRenderRoleBodyErrorFallbackTimestampAndChronologicalOrder() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversation = try decode(AgentConversation.self, conversationData)
        viewModel.agentConversationMessages = try decode([AgentConversationMessage].self, messageData)

        let rows = viewModel.agentConversationRows(fallbackId: "conversation", agentSystemUserId: "agent")
        XCTAssertEqual(rows.map(\.title), ["Conversation", "User", "Agent", "User"])
        XCTAssertTrue(rows[1].detail.contains("First"))
        XCTAssertTrue(rows[2].detail.contains("Fallback error"))
        XCTAssertTrue(rows[3].detail.contains("No results"))
        XCTAssertNotEqual(
            rows[1].localizedDetail(locale: Locale(identifier: "en_US"), timeZone: .gmt),
            rows[1].localizedDetail(locale: Locale(identifier: "es_ES"), timeZone: .gmt)
        )

    }

    func testWhitespaceConversationTitlesUseLocalizedFallbacks() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversationListResults = try [decode(
            AgentConversationSummary.self,
            #"{"id":"conversation","title":"  ","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        )]
        viewModel.agentConversation = try decode(
            AgentConversation.self,
            #"{"id":"conversation","channel_type":"agent","title":"  ","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"last_response_id":null}"#
        )

        XCTAssertEqual(
            viewModel.agentConversationListRows().first?.title,
            "Agent conversation"
        )
        XCTAssertEqual(
            viewModel.agentConversationRows(fallbackId: "conversation", agentSystemUserId: nil).first?.title,
            "Agent conversation conversation"
        )
    }

    func testEmptyAgentDetailRendersAnEmptyState() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.state = .loaded
        viewModel.agentDetail = try decode(AgentDetailResponse.self, detailData)
        let surface = try makeSurface(viewModel: viewModel, isSignedIn: true, isAdministrator: true)

        XCTAssertNoThrow(try surface.inspect().find(text: "No results"))
    }

    func testDetailRendersContinuationErrorAndRetry() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.state = .loaded
        viewModel.agentDetail = try decode(AgentDetailResponse.self, detailData)
        viewModel.agentConversationListResults = try JSONDecoder.vouchaFixtureDecoder.decode(
            AgentConversationListResponse.self,
            from: conversationListData(id: "current", hasNextPage: true)
        ).results
        viewModel.agentConversationListPageInfo = .init(
            hasNextPage: true,
            endCursor: "cursor-current"
        )
        viewModel.agentConversationsPageErrorMessage = .verbatim("offline")
        let surface = try makeSurface(viewModel: viewModel, isSignedIn: true, isAdministrator: true)

        XCTAssertNoThrow(try surface.inspect().find(text: "offline"))
        XCTAssertNoThrow(try surface.inspect().find(button: "Try Again"))
        XCTAssertNoThrow(try surface.inspect().find(text: "current"))
    }

    func testEmptyTranscriptRendersConversationAndEmptyState() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.state = .loaded
        viewModel.agentConversation = try decode(AgentConversation.self, conversationData)
        let surface = try makeSurface(viewModel: viewModel, isSignedIn: true, isAdministrator: true)

        XCTAssertNoThrow(try surface.inspect().find(text: "Conversation"))
        XCTAssertNoThrow(try surface.inspect().find(text: "No results"))
    }

    func testFilterSearchAndClearResetTheConversationPage() async throws {
        let viewModel = try makeAgentDetailViewModel()
        CannedFeedURLProtocol.handlers[agentConversationsPath] = (conversationListData(id: "filtered"), 200)

        await viewModel.reloadAgentConversations(filter: .username("fixture"))
        XCTAssertEqual(viewModel.agentConversationListResults.map(\.id), ["filtered"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=2&username=fixture")

        await viewModel.reloadAgentConversations(filter: nil)
        XCTAssertNil(viewModel.agentConversationFilter)
        XCTAssertEqual(viewModel.agentConversationListResults.map(\.id), ["filtered"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=2")
    }

    func testFilteredContinuationPreservesFilterAndRetryKeepsRows() async throws {
        let viewModel = try makeAgentDetailViewModel()
        CannedFeedURLProtocol.handlers[agentConversationsPath] = (
            conversationListData(id: "first", hasNextPage: true),
            200
        )
        await viewModel.reloadAgentConversations(filter: .username("fixture"))
        let firstRows = viewModel.rows

        CannedFeedURLProtocol.handlers[agentConversationsPath] = (Data(#"{"message":"failed"}"#.utf8), 500)
        await viewModel.loadMoreAgentConversations()
        XCTAssertEqual(viewModel.rows, firstRows)
        XCTAssertNotNil(viewModel.agentConversationsPageErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=2&after=cursor-first&username=fixture")

        CannedFeedURLProtocol.handlers[agentConversationsPath] = (conversationListData(id: "second"), 200)
        await viewModel.loadMoreAgentConversations()
        XCTAssertEqual(viewModel.agentConversationListResults.map(\.id), ["first", "second"])
        XCTAssertNil(viewModel.agentConversationsPageErrorMessage)
    }

    func testFailedInitialFilterRequestRetriesFromTheFirstPage() async throws {
        let viewModel = try makeAgentDetailViewModel()
        CannedFeedURLProtocol.handlers[agentConversationsPath] = (Data(#"{"message":"failed"}"#.utf8), 500)

        await viewModel.reloadAgentConversations(filter: .username("fixture"))
        XCTAssertTrue(viewModel.agentConversationListResults.isEmpty)
        XCTAssertTrue(viewModel.canLoadMoreAgentConversations)

        CannedFeedURLProtocol.handlers[agentConversationsPath] = (conversationListData(id: "retried"), 200)
        await viewModel.loadMoreAgentConversations()
        XCTAssertEqual(viewModel.agentConversationListResults.map(\.id), ["retried"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=2&username=fixture")
    }

    func testReplacedFilterIgnoresStaleResponse() async throws {
        let viewModel = try makeAgentDetailViewModel()
        CannedFeedURLProtocol.queuedHandlers[agentConversationsPath] = [
            (conversationListData(id: "stale"), 200, 0.1),
            (conversationListData(id: "current"), 200, 0)
        ]

        let stale = Task { await viewModel.reloadAgentConversations(filter: .username("stale")) }
        let current = Task { await viewModel.reloadAgentConversations(filter: .username("current")) }
        await stale.value
        await current.value

        XCTAssertEqual(viewModel.agentConversationFilter, .username("current"))
        XCTAssertEqual(viewModel.agentConversationListResults.map(\.id), ["current"])
    }

    private func makeSurface(
        viewModel: NativeRouteSurfaceViewModel? = nil,
        isSignedIn: Bool,
        isAdministrator: Bool
    ) throws -> NativeEngineeringAgentsSurface {
        let model: NativeRouteSurfaceViewModel = if let viewModel {
            viewModel
        } else {
            try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        }
        return NativeEngineeringAgentsSurface(
            viewModel: model,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            onNavigate: { _ in },
            showSignIn: {}
        )
    }

    private func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        try JSONDecoder.vouchaFixtureDecoder.decode(type, from: Data(json.utf8))
    }

    private func repoSource(_ file: String, filePath: StaticString = #filePath) throws -> String {
        let relative = file.contains("/") ? file : "swift-clients/ui/Sources/VouchaFeatures/NativeParity/\(file)"
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let source = URL(fileURLWithPath: "\(filePath)", relativeTo: current).standardizedFileURL
        var directory = source.deletingLastPathComponent()
        var candidates = [current]
        for _ in 0 ..< 16 {
            candidates.append(directory)
            directory.deleteLastPathComponent()
        }
        for root in candidates {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
        }
        throw XCTSkip("Could not find \(relative) from the test working directory")
    }

    private var agentConversationsPath: String {
        "/api/v1/agents/helper/conversations"
    }

    private func makeAgentDetailViewModel() throws -> NativeRouteSurfaceViewModel {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/helper")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents), client: makeClient(), routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentDetail = try decode(AgentDetailResponse.self, detailData)
        return viewModel
    }

    private func conversationListData(id: String, hasNextPage: Bool = false) -> Data {
        let endCursor = hasNextPage ? "\"cursor-\(id)\"" : "null"
        return Data("""
        {"results":[{"id":"\(id)","title":"\(
            id
        )","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}],"page_info":{"has_next_page":\(
            hasNextPage
        ),"end_cursor":\(endCursor),"start_cursor":"start-\(id)"},"users":{}}
        """.utf8)
    }

    private var detailData: String {
        #"{"agent":{"id":"agent","system_user_id":"system","agent_type":"helper","activated_at":"2026-07-01T10:00:00Z","deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":"helper","moderator":null},"user":null}"#
    }

    private var conversationData: String {
        #"{"id":"conversation","channel_type":"agent","title":"Conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"last_response_id":null}"#
    }

    private var messageData: String {
        #"[{"id":"first","conversation_id":"conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"First"}},{"id":"error","conversation_id":"conversation","created_at":"2026-07-01T10:01:00Z","created_by_id":"agent","updated_at":"2026-07-01T10:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","error":"Fallback error"}},{"id":"empty","conversation_id":"conversation","created_at":"2026-07-01T10:02:00Z","created_by_id":"tool","updated_at":"2026-07-01T10:02:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"tool"}}]"#
    }
}
