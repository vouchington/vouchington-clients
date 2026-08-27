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
}
