import Foundation
@testable import VouchaCore
import XCTest

final class OpenAICompatibleResponsesClientTests: XCTestCase {
    override func setUp() {
        super.setUp()
        ResponsesURLProtocol.responseData = Data(#"{"output_text":"Local answer"}"#.utf8)
        ResponsesURLProtocol.statusCode = 200
        ResponsesURLProtocol.capturedRequest = nil
        ResponsesURLProtocol.capturedBody = nil
    }

    func testGenerateAssistantResponseSendsResponsesRequest() async throws {
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])
        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [.init(role: "user", content: "Context")],
            configuration: configuration(endpoint: "http://localhost:2999/v1"),
            apiKey: "test-key"
        )

        XCTAssertEqual(response, "Local answer")
        let request = try XCTUnwrap(ResponsesURLProtocol.capturedRequest)
        XCTAssertEqual(request.url?.path, "/v1/responses")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer test-key")
        let body = try String(data: XCTUnwrap(ResponsesURLProtocol.capturedBody), encoding: .utf8)
        XCTAssertTrue(body?.contains(#""model":"gpt-oss""#) == true)
        XCTAssertTrue(body?.contains(#""content":"Hello""#) == true)
    }

    func testGenerateAssistantResponseValidatesEndpointAndModel() async {
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])

        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                configuration: configuration(endpoint: "not a url"),
                apiKey: nil
            )
            XCTFail("Expected invalid endpoint")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .invalidEndpoint)
        }
        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                configuration: configuration(endpoint: "http://localhost:2999/v1", selectedModel: " "),
                apiKey: nil
            )
            XCTFail("Expected missing model")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .missingModel)
        }
    }

    func testGenerateAssistantResponseThrowsForHTTPFailure() async {
        ResponsesURLProtocol.statusCode = 502
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])

        do {
            _ = try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                configuration: configuration(endpoint: "http://localhost:2999/v1"),
                apiKey: nil
            )
            XCTFail("Expected request failure")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .requestFailed(statusCode: 502))
        }
    }

    func testGenerateAssistantResponsePreservesExplicitTaskCancellation() async {
        let client = OpenAICompatibleResponsesClient(protocolClasses: [CancellationURLProtocol.self])
        let task = Task {
            try await client.generateAssistantResponse(
                message: "Hello",
                history: [],
                configuration: configuration(endpoint: "http://localhost:2999/v1"),
                apiKey: "test-key"
            )
        }

        task.cancel()

        do {
            _ = try await task.value
            XCTFail("Expected cancellation")
        } catch is CancellationError {
            // Expected: explicit task cancellation must not be reported as a redirect.
        } catch {
            XCTFail("Expected CancellationError, got \(error)")
        }
    }

    func testRedirectDelegateRejectsBeforeSendingCredentialsToNewHost() throws {
        let delegate = LocalLLMURLSessionDelegate()
        let session = URLSession(configuration: .ephemeral)
        let task = try session.dataTask(with: XCTUnwrap(URL(string: "https://local.example.test")))
        let response = try XCTUnwrap(try HTTPURLResponse(
            url: XCTUnwrap(URL(string: "https://local.example.test")),
            statusCode: 307,
            httpVersion: "HTTP/1.1",
            headerFields: nil
        ))
        var redirectedRequest: URLRequest?

        try delegate.urlSession(
            session,
            task: task,
            willPerformHTTPRedirection: response,
            newRequest: URLRequest(url: XCTUnwrap(URL(string: "https://remote.example.test"))),
            completionHandler: { redirectedRequest = $0 }
        )

        XCTAssertNil(redirectedRequest)
    }

    func testGenerateAssistantResponseReturnsNilForBlankTopLevelOutputText() async throws {
        ResponsesURLProtocol.responseData = Data(#"{"output_text":"  \n\t  "}"#.utf8)
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])

        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            configuration: configuration(endpoint: "http://localhost:2999/v1/responses"),
            apiKey: nil
        )

        XCTAssertNil(response)
    }

    func testGenerateAssistantResponseIgnoresOutputWithoutContent() async throws {
        ResponsesURLProtocol.responseData = Data(#"{"output":[{},{"content":[{"text":"Nested answer"}]}]}"#.utf8)
        let client = OpenAICompatibleResponsesClient(protocolClasses: [ResponsesURLProtocol.self])

        let response = try await client.generateAssistantResponse(
            message: "Hello",
            history: [],
            configuration: configuration(endpoint: "http://localhost:2999/v1/responses"),
            apiKey: nil
        )

        XCTAssertEqual(response, "Nested answer")
    }

    private func configuration(
        endpoint: String,
        selectedModel: String = "gpt-oss"
    ) -> LocalLLMConfiguration {
        let endpointID = UUID()
        return LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                endpoint: endpoint,
                modelNames: ["gpt-oss"],
                selectedModelName: selectedModel
            )],
            selectedEndpointID: endpointID
        )
    }
}
