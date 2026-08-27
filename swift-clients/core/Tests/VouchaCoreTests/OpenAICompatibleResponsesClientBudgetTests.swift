import Foundation
@testable import VouchaCore
import XCTest

final class OpenAIResponsesClientBudgetTests: XCTestCase {
    override func setUp() {
        super.setUp()
        ResponsesURLProtocol.responseData = Data(#"{"output_text":"Local answer"}"#.utf8)
        ResponsesURLProtocol.statusCode = 200
        ResponsesURLProtocol.capturedRequest = nil
        ResponsesURLProtocol.capturedBody = nil
    }

    func testGenerateAssistantResponseBoundsAggregateInputAndReservesLatestMessage() async throws {
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])
        let latest = String(repeating: "L", count: 4_000)
        _ = try await client.generateAssistantResponse(
            message: latest,
            history: [
                .init(role: "user", content: "oldest-" + String(repeating: "O", count: 8_000)),
                .init(role: "assistant", content: "recent-" + String(repeating: "R", count: 9_000))
            ],
            configuration: configuration(),
            apiKey: nil
        )

        let body = try XCTUnwrap(ResponsesURLProtocol.capturedBody)
        let payload = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
        let input = try XCTUnwrap(payload["input"] as? [[String: String]])
        let contents = try input.map { try XCTUnwrap($0["content"]) }

        XCTAssertLessThanOrEqual(contents.reduce(0) { $0 + $1.count }, 12_000)
        XCTAssertEqual(contents.last, latest)
        XCTAssertTrue(contents.contains { $0.hasPrefix("recent-") })
        XCTAssertFalse(contents.contains { $0.hasPrefix("oldest-") })
    }

    func testGenerateAssistantResponseParsesOutputContentText() async throws {
        ResponsesURLProtocol.responseData = Data("""
        {
          "output": [
            { "content": [{ "text": "Nested answer" }] }
          ]
        }
        """.utf8)
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            configuration: configuration(),
            apiKey: nil
        )

        XCTAssertEqual(response, "Nested answer")
    }

    func testGenerateAssistantResponseReturnsNilForEmptyNestedOutput() async throws {
        ResponsesURLProtocol.responseData = Data(#"{"output":[{"content":[{"text":"  "}]}]}"#.utf8)
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])

        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            configuration: configuration(),
            apiKey: nil
        )

        XCTAssertNil(response)
    }

    private func configuration() -> LocalLLMConfiguration {
        let endpointID = UUID()
        return LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                endpoint: "http://localhost:2999/v1/responses",
                modelNames: ["gpt-oss"],
                selectedModelName: "gpt-oss"
            )],
            selectedEndpointID: endpointID
        )
    }
}
