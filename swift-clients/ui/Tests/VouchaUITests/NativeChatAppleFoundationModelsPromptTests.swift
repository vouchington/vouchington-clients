import Foundation
@testable import VouchaFeatures
import XCTest

final class NativeChatAppleFoundationModelsPromptTests: XCTestCase {
    func testAssistantPromptBoundsTranscriptWhileRetainingTheLatestMessage() {
        let oldestContent = String(repeating: "old-context ", count: 30)
        let recentContent = "Recent context that should remain available."
        let latestMessage = "Please use the reserved latest-message space."
        let prompt = NativeChatAppleFoundationModelsProvider.assistantPrompt(
            message: latestMessage,
            history: [
                .init(id: "old", role: .user, content: oldestContent),
                .init(id: "recent", role: .assistant, content: recentContent)
            ],
            maximumCharacters: 240
        )

        XCTAssertLessThanOrEqual(prompt.count, 240)
        XCTAssertTrue(prompt.contains("Latest user message:\n\(latestMessage)"))
        XCTAssertTrue(prompt.contains("assistant: \(recentContent)"))
        XCTAssertFalse(prompt.contains(oldestContent))
    }

    func testAssistantPromptTruncatesAnOversizedLatestMessageWithinTheBudget() {
        let prompt = NativeChatAppleFoundationModelsProvider.assistantPrompt(
            message: String(repeating: "latest ", count: 100),
            history: [.init(id: "history", role: .user, content: "Context")],
            maximumCharacters: 180
        )

        XCTAssertLessThanOrEqual(prompt.count, 180)
        XCTAssertTrue(prompt.contains("Latest user message:"))
        XCTAssertFalse(prompt.contains("user: Context"))
    }

    @available(macOS 26.4, iOS 26.4, *)
    func testToolPromptBoundsUTF8BytesForOversizedGraphemeClusters() {
        let cluster = "a" + String(repeating: "\u{0301}", count: 2_000)
        let limit = NativeChatFoundationModelsToolSelection.maximumPromptCharacters
        let context = NativeChatAppleFoundationModelsProvider.boundedAssistantContext(
            message: cluster + " DM", history: [
                .init(id: "history", role: .user, content: cluster + " cards")
            ], maximumCharacters: limit
        )
        let prompt = NativeChatAppleFoundationModelsProvider.assistantPrompt(
            message: cluster + " DM", history: [
                .init(id: "history", role: .user, content: cluster + " cards")
            ], maximumCharacters: limit
        )
        XCTAssertLessThanOrEqual(prompt.utf8.count, limit)
        XCTAssertLessThanOrEqual(context.selectionHistory.utf8.count + context.latestMessage.utf8.count, limit)
        XCTAssertFalse(prompt.contains(cluster))
    }
}
