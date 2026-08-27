import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeEngineeringAgentTranscriptIdentityTests: NativeRouteSurfaceViewModelTestCase {
    func testTranscriptHeaderLocalizesConversationCreationDateWithoutExposingTheID() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversation = try decode(AgentConversation.self, conversationJSON)

        let header = try XCTUnwrap(transcriptRows(from: viewModel).first)
        let locale = Locale(identifier: "en_US")
        let createdAt = try XCTUnwrap(viewModel.agentConversation?.createdAt)
        let expectedDate = UiMessages.date(
            createdAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: .gmt
        )
        let localizedDetail = header.localizedDetail(locale: locale, timeZone: .gmt)

        XCTAssertTrue(localizedDetail.contains(expectedDate))
        XCTAssertFalse(localizedDetail.contains("conversation-1"))
        XCTAssertNotEqual(
            localizedDetail,
            header.localizedDetail(locale: Locale(identifier: "es_ES"), timeZone: .gmt)
        )
    }

    func testDirectoryAndConversationListRowIDsRemainStableWhenPagesAppend() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        let agents = try decode([AgentSummary].self, agentsJSON)
        viewModel.agentDirectoryResults = agents

        XCTAssertEqual(viewModel.agentDirectoryRows().map(\.id), [
            "agent-directory:agent-1",
            "agent-directory:agent-2"
        ])
        XCTAssertEqual(viewModel.agentDirectoryRows().map(\.id), [
            "agent-directory:agent-1",
            "agent-directory:agent-2"
        ])
        viewModel.agentDirectoryResults = try agents + [decode(AgentSummary.self, appendedAgentJSON)]
        XCTAssertEqual(viewModel.agentDirectoryRows().map(\.id), [
            "agent-directory:agent-1",
            "agent-directory:agent-2",
            "agent-directory:agent-3"
        ])

        let conversations = try decode([AgentConversationSummary].self, conversationsJSON)
        viewModel.agentConversationListResults = conversations
        XCTAssertEqual(viewModel.agentConversationListRows().map(\.id), [
            "agent-conversation-list:conversation-1",
            "agent-conversation-list:conversation-2"
        ])
        XCTAssertEqual(viewModel.agentConversationListRows().map(\.id), [
            "agent-conversation-list:conversation-1",
            "agent-conversation-list:conversation-2"
        ])
        viewModel.agentConversationListResults = try conversations + [
            decode(AgentConversationSummary.self, appendedConversationJSON)
        ]
        XCTAssertEqual(viewModel.agentConversationListRows().map(\.id), [
            "agent-conversation-list:conversation-1",
            "agent-conversation-list:conversation-2",
            "agent-conversation-list:conversation-3"
        ])
    }

    func testTranscriptRowIDsRemainStableAcrossLoadingErrorAndPaginationRerenders() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversation = try decode(AgentConversation.self, conversationJSON)
        let initialMessages = try decode([AgentConversationMessage].self, initialMessagesJSON)
        viewModel.agentConversationMessages = initialMessages

        let expectedInitialIDs = [
            "agent-conversation:conversation-1:header",
            "agent-conversation:conversation-1:message:message-1",
            "agent-conversation:conversation-1:message:message-2"
        ]
        XCTAssertEqual(transcriptRows(from: viewModel).map(\.id), expectedInitialIDs)

        viewModel.state = .loading
        XCTAssertEqual(transcriptRows(from: viewModel).map(\.id), expectedInitialIDs)

        viewModel.state = .loaded
        viewModel.agentConversationPaginationErrorMessage = .verbatim("offline")
        XCTAssertEqual(transcriptRows(from: viewModel).map(\.id), expectedInitialIDs)

        let olderMessage = try decode(AgentConversationMessage.self, olderMessageJSON)
        viewModel.agentConversationMessages = [olderMessage] + initialMessages
        XCTAssertEqual(transcriptRows(from: viewModel).map(\.id), [
            "agent-conversation:conversation-1:header",
            "agent-conversation:conversation-1:message:message-0",
            "agent-conversation:conversation-1:message:message-1",
            "agent-conversation:conversation-1:message:message-2"
        ])
    }

    func testTranscriptUsesDeletedAuthorFallbackWhenMessageAuthorWasDeleted() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        viewModel.agentConversation = try decode(AgentConversation.self, conversationJSON)
        let deletedAuthorMessage = try decode(AgentConversationMessage.self, deletedAuthorMessageJSON)
        viewModel.agentConversationMessages = [deletedAuthorMessage]

        let row = try XCTUnwrap(transcriptRows(from: viewModel).last)
        XCTAssertEqual(
            row.localizedTitle(locale: Locale(identifier: "en_US")),
            UiMessages.string(.nativeSwiftPresentationValuesDeleted, locale: Locale(identifier: "en_US"))
        )
    }

    func testConversationListUsesDeletedCreatorFallback() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .engineeringAgents), client: nil)
        let conversation = try decode(
            AgentConversationSummary.self,
            #"{"id":"deleted","title":"Conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":null,"updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        )
        viewModel.agentConversationListResults = [conversation]

        let row = try XCTUnwrap(viewModel.agentConversationListRows().first)
        XCTAssertTrue(row.localizedDetail(locale: Locale(identifier: "en_US"), timeZone: .gmt).contains("Deleted"))
    }

    private func transcriptRows(from viewModel: NativeRouteSurfaceViewModel) -> [NativeRouteDestinationRow] {
        viewModel.agentConversationRows(fallbackId: "conversation-1", agentSystemUserId: "agent")
    }

    private func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        try JSONDecoder.vouchaFixtureDecoder.decode(type, from: Data(json.utf8))
    }

    private var conversationJSON: String {
        #"{"id":"conversation-1","channel_type":"agent","title":"Conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"last_response_id":null}"#
    }

    private var agentsJSON: String {
        #"[{"id":"agent-1","system_user_id":"user-1","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null},{"id":"agent-2","system_user_id":"user-2","agent_type":"reviewer","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:01:00Z","updated_at":"2026-07-01T10:01:00Z","deleted_at":null,"slug":null,"moderator":null}]"#
    }

    private var appendedAgentJSON: String {
        #"{"id":"agent-3","system_user_id":"user-3","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:02:00Z","updated_at":"2026-07-01T10:02:00Z","deleted_at":null,"slug":null,"moderator":null}"#
    }

    private var conversationsJSON: String {
        #"[{"id":"conversation-1","title":"First","created_at":"2026-07-01T10:00:00Z","created_by_id":"user-1","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null},{"id":"conversation-2","title":"Second","created_at":"2026-07-01T10:01:00Z","created_by_id":"user-2","updated_at":"2026-07-01T10:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}]"#
    }

    private var appendedConversationJSON: String {
        #"{"id":"conversation-3","title":"Third","created_at":"2026-07-01T10:02:00Z","created_by_id":"user-3","updated_at":"2026-07-01T10:02:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
    }

    private var initialMessagesJSON: String {
        #"[{"id":"message-1","conversation_id":"conversation-1","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"First"}},{"id":"message-2","conversation_id":"conversation-1","created_at":"2026-07-01T10:01:00Z","created_by_id":"agent","updated_at":"2026-07-01T10:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"Second"}}]"#
    }

    private var olderMessageJSON: String {
        #"{"id":"message-0","conversation_id":"conversation-1","created_at":"2026-07-01T09:59:00Z","created_by_id":"user","updated_at":"2026-07-01T09:59:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"Older"}}"#
    }

    private var deletedAuthorMessageJSON: String {
        #"{"id":"message-deleted-author","conversation_id":"conversation-1","created_at":"2026-07-01T10:03:00Z","created_by_id":null,"updated_at":"2026-07-01T10:03:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"Deleted author"}}"#
    }
}
