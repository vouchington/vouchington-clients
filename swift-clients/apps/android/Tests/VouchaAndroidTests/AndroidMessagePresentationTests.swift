import Foundation
@testable import VouchaAndroid
@testable import VouchaAPI
import VouchaLocalization
import VouchaModels
import XCTest

final class AndroidMessagePresentationTests: XCTestCase {
    func testIncompleteCanonicalMessageRetainsLocalizedInterruption() throws {
        let message = try decodeMessage(AndroidTranscriptFixtures.data("native.chat.incomplete"))
        let presentation = AndroidChatMessage(transcriptMessage: message)

        XCTAssertEqual(presentation.id, message.id)
        XCTAssertEqual(presentation.role, "assistant")
        XCTAssertEqual(presentation.content, "")
        XCTAssertEqual(presentation.error, .message(.nativeSwiftChatResponseInterrupted))
    }

    func testServerErrorWithNullContentRemainsVisibleVerbatim() throws {
        let message = try decodeMessage(withContent([
            "role": "assistant",
            "content": NSNull(),
            "error": "Provider unavailable"
        ]))
        let presentation = AndroidChatMessage(transcriptMessage: message)

        XCTAssertEqual(presentation.content, "")
        XCTAssertEqual(presentation.error, .verbatim("Provider unavailable"))
    }

    func testPartialTextAndErrorAreBothRetained() throws {
        let message = try decodeMessage(withContent([
            "role": "assistant",
            "content": "Partial reply",
            "error": "Provider unavailable"
        ]))
        let presentation = AndroidChatMessage(transcriptMessage: message)

        XCTAssertEqual(presentation.content, "Partial reply")
        XCTAssertEqual(presentation.error, .verbatim("Provider unavailable"))
    }

    func testCompletedCanonicalMessageHasNoFailure() throws {
        let message = try decodeMessage(AndroidTranscriptFixtures.data("native.chat.page-1"))
        let presentation = AndroidChatMessage(transcriptMessage: message)

        XCTAssertEqual(presentation.content, message.content.displayText)
        XCTAssertNil(presentation.error)
    }

    private func decodeMessage(_ data: Data) throws -> ChatMessage {
        let response = try APIClient.makeDecoder().decode(ChatMessagesResponse.self, from: data)
        return try XCTUnwrap(response.results.first)
    }

    private func withContent(_ content: [String: Any]) throws -> Data {
        let data = try AndroidTranscriptFixtures.data("native.chat.incomplete")
        var envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var messages = try XCTUnwrap(envelope["results"] as? [[String: Any]])
        messages[0]["content"] = content
        envelope["results"] = messages
        return try JSONSerialization.data(withJSONObject: envelope)
    }
}
