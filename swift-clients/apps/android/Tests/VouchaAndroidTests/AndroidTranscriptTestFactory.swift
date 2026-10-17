import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

struct AndroidTranscriptTestClient {
    let host: String
    let recorder: AndroidTranscriptRecorder
    let client: APIClient
}

enum AndroidTranscriptTestFactory {
    static let newConversationID = "0198ffff-0000-7000-8000-000000000001"
    static let generatedUserMessageID = "0198ffff-0001-7000-8000-000000000001"
    static let generatedAssistantMessageID = "0198ffff-0001-7000-8000-000000000002"

    @MainActor
    static func makeViewModel(runtime: FakeAICoreRuntime) -> ViewModel {
        ViewModel(
            runtime: runtime,
            secretStore: MemorySecrets(),
            settingsStore: LocalLLMSettingsStore(
                fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
                secretStore: InMemoryLocalLLMSecretStore()
            )
        )
    }

    static func makeClient() throws -> AndroidTranscriptTestClient {
        let host = "\(UUID().uuidString.lowercased()).transcript.test"
        let recorder = AndroidTranscriptRecorder()
        AndroidTranscriptURLProtocol.install(recorder, host: host)
        let configuration = URLSessionConfiguration.ephemeral
        let cookies = try XCTUnwrap(configuration.httpCookieStorage)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .domain: host,
            .path: "/",
            .name: "st",
            .value: "fixture-session"
        ]))
        cookies.setCookie(cookie)
        let baseURL = try XCTUnwrap(URL(string: "http://\(host)"))
        let client = APIClient(
            config: AppConfig(baseURL: baseURL),
            cookieStorage: cookies,
            protocolClasses: [AndroidTranscriptURLProtocol.self],
            bootstrapSession: false,
            sessionConfiguration: configuration
        )
        return AndroidTranscriptTestClient(host: host, recorder: recorder, client: client)
    }

    static func enqueueEmptyConversationCreation(on recorder: AndroidTranscriptRecorder) {
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8)
        )
        recorder.enqueue(
            path: "/api/v1/conversations",
            data: conversationResponse(id: newConversationID, title: "New conversation")
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(newConversationID)/messages",
            data: emptyMessagePageResponse()
        )
    }

    static func enqueueGeneratedTurn(
        on recorder: AndroidTranscriptRecorder,
        conversationID: String = newConversationID,
        userText: String,
        assistantText: String
    ) {
        recorder.enqueue(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat",
            responseBuilder: generatedChatResponseBuilder(
                conversationID: conversationID,
                userText: userText,
                assistantText: assistantText
            )
        )
    }

    static func enqueueExistingConversation(on recorder: AndroidTranscriptRecorder) throws {
        try recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try recorder.enqueue(
            path: "/api/v1/my/conversations/\(newConversationID)/messages",
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )
    }

    static func enqueueConversationSwitchFlow(
        on recorder: AndroidTranscriptRecorder,
        firstID: String,
        secondID: String
    ) {
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: twoConversationListResponse(firstID: firstID, secondID: secondID)
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            data: emptyMessagePageResponse()
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(secondID)/messages",
            data: messagePageResponse(
                id: "second-conversation-message",
                conversationID: secondID,
                content: "Second conversation history"
            )
        )
    }

    static func enqueueHeldGeneratedTurn(
        on recorder: AndroidTranscriptRecorder,
        conversationID: String,
        userText: String,
        assistantText: String
    ) {
        recorder.enqueue(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat",
            data: generatedChatResponse(
                conversationID: conversationID,
                userText: userText,
                assistantText: assistantText
            ),
            held: true
        )
    }

}

extension Data {
    func jsonObject() throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: self) as? [String: Any])
    }
}
