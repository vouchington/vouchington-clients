@testable import VouchaModels
import XCTest

final class ApiFixturePaginationDecodingTests: XCTestCase {
    private let decoder = makeVouchaDecoder()

    func testDecodesOpaqueAgentConversationHistoryFixtures() throws {
        let first = try decoder.decode(
            AgentConversationDetailResponse.self,
            from: ApiFixtureLoader.data("native.agents.conversation.default")
        )
        let older = try decoder.decode(
            AgentConversationDetailResponse.self,
            from: ApiFixtureLoader.data("native.agents.conversation.page-2")
        )

        XCTAssertEqual(first.results.map(\.id), [
            "00000000-0000-7000-8000-000000000201",
            "00000000-0000-7000-8000-000000000202"
        ])
        XCTAssertTrue(first.pageInfo.hasNextPage)
        XCTAssertTrue(first.pageInfo.endCursor?.hasPrefix("eyJ") == true)
        XCTAssertNil(first.results.first?.createdById)
        XCTAssertEqual(older.results.first?.content?.content, "Earlier context")
        XCTAssertFalse(older.pageInfo.hasNextPage)
        XCTAssertNil(older.pageInfo.endCursor)
    }

    func testDecodesOpaqueMessagingContinuationFixtures() throws {
        let firstConversations = try decoder.decode(
            Page<DirectConversation>.self,
            from: ApiFixtureLoader.data("native.messages.conversations.default")
        )
        let olderConversations = try decoder.decode(
            Page<DirectConversation>.self,
            from: ApiFixtureLoader.data("native.messages.conversations.page-2")
        )
        XCTAssertTrue(firstConversations.pageInfo.hasNextPage)
        XCTAssertTrue(firstConversations.pageInfo.endCursor?.hasPrefix("eyJ") == true)
        XCTAssertEqual(olderConversations.results.map(\.id), ["00000000-0000-7000-8000-000000000103"])
        XCTAssertFalse(olderConversations.pageInfo.hasNextPage)

        let firstMessages = try decoder.decode(
            Page<DirectMessage>.self,
            from: ApiFixtureLoader.data("native.messages.thread.default")
        )
        let olderMessages = try decoder.decode(
            Page<DirectMessage>.self,
            from: ApiFixtureLoader.data("native.messages.thread.page-2")
        )
        XCTAssertTrue(firstMessages.pageInfo.hasNextPage)
        XCTAssertTrue(firstMessages.pageInfo.endCursor?.hasPrefix("eyJ") == true)
        XCTAssertEqual(olderMessages.results.first?.bodyText, "Earlier context")
        XCTAssertFalse(olderMessages.pageInfo.hasNextPage)
    }

    func testDecodesOpaqueModmailAndSavedReplyContinuationFixtures() throws {
        let threads = try decoder.decode(
            CommunityModmailThreadsResponse.self,
            from: ApiFixtureLoader.data("web.communities.modmail.page-2")
        )
        let messages = try decoder.decode(
            CommunityModmailMessagesResponse.self,
            from: ApiFixtureLoader.data("web.communities.modmail-messages.page-2")
        )
        let replies = try decoder.decode(
            CommunitySavedRepliesResponse.self,
            from: ApiFixtureLoader.data("web.communities.saved-replies.page-2")
        )

        XCTAssertEqual(threads.results.first?.id, "00000000-0000-7000-8000-000000000502")
        XCTAssertEqual(threads.results.first?.channelType, "modmail")
        XCTAssertEqual(threads.results.first?.title, "")
        XCTAssertNil(threads.results.first?.assignedAt)
        XCTAssertNil(threads.results.first?.resolvedById)
        XCTAssertEqual(threads.results.first?.createdById, "user-1")
        XCTAssertEqual(messages.results.first?.bodyText, "Earlier modmail context.")
        XCTAssertEqual(replies.results.first?.orderIndex, 1)
        XCTAssertFalse(threads.pageInfo.hasNextPage)
        XCTAssertFalse(messages.pageInfo.hasNextPage)
        XCTAssertFalse(replies.pageInfo.hasNextPage)
    }
}
