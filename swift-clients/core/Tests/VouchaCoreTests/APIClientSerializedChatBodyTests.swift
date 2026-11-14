import Foundation
@testable import VouchaAPI
import VouchaCore
import XCTest

final class APIClientSerializedChatBodyTests: XCTestCase {
    func testClientGeneratedChatUsesOriginalSerializedBodyBytes() async throws {
        let client = try APIClient(config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))))
        let conversationID = "0198ffff-0000-7000-8000-000000000001"
        let serializedBody = Data(
            #"{"model_provider":"android_aicore","assistant_content":"Answer","assistant_message_id":"assistant","user_message_id":"user","message":"Question","model_name":"android-aicore-system"}"#
                .utf8
        )
        let endpoint = Endpoint.clientGeneratedChat(conversationId: conversationID, serializedBody: serializedBody)

        let request = try await client.buildRequest(endpoint.withHeaders(["x-test": "retry"]))

        XCTAssertEqual(request.httpBody, serializedBody)
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Content-Type"), "application/json")
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-test"), "retry")
        XCTAssertEqual(request.url?.path, "/api/v1/conversations/\(conversationID)/client-generated-chat")
    }

    func testTypedChatBodyStillUsesSnakeCaseWithHeaders() async throws {
        let client = try APIClient(config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))))
        let endpoint = Endpoint.clientGeneratedChat(
            conversationId: "0198ffff-0000-7000-8000-000000000001",
            message: "Question",
            messageIds: (user: "user", assistant: "assistant"),
            assistantContent: "Answer",
            model: (provider: "android_aicore", name: "android-aicore-system")
        )

        let request = try await client.buildRequest(endpoint.withHeaders(["x-test": "typed"]))
        let body = try XCTUnwrap(request.httpBody)
        let decoded = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: String])

        XCTAssertEqual(decoded["user_message_id"], "user")
        XCTAssertEqual(decoded["assistant_message_id"], "assistant")
        XCTAssertEqual(decoded["model_provider"], "android_aicore")
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-test"), "typed")
    }
}
