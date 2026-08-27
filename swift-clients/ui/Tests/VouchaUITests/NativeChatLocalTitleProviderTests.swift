import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatLocalTitleProviderTests: XCTestCase {
    func testStatusAndGenerationUseOpenAICompatibleSettings() async throws {
        LocalLLMTestURLProtocol.reset(responseData: Data(#"{"output_text":"\"Local title\""}"#.utf8))
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(),
            apiKey: "local-key"
        )
        let provider = NativeChatLocalTitleProvider(
            settingsStore: store,
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )

        XCTAssertEqual(provider.status, .init(isAvailable: true, detail: .verbatim("gpt-oss")))

        let response = try await provider.generateAssistantResponse(
            to: "Hello",
            history: [
                .init(id: "1", role: .assistant, content: "Prior", isStreaming: false),
                .init(id: "2", role: .user, content: "   ", isStreaming: false),
                .init(id: "3", role: .assistant, content: "streaming", isStreaming: true)
            ]
        )
        let title = try await provider.generateTitle(from: [
            .init(id: "4", role: .user, content: "Name this", isStreaming: false)
        ])

        XCTAssertEqual(response?.content, "\"Local title\"")
        XCTAssertEqual(response?.modelProvider, "openai_compatible")
        XCTAssertEqual(response?.modelName, "gpt-oss")
        XCTAssertEqual(title, "Local title")
        XCTAssertEqual(
            LocalLLMTestURLProtocol.capturedRequest?.value(forHTTPHeaderField: "Authorization"),
            "Bearer local-key"
        )

        let updatedConfiguration = makeLocalLLMConfiguration(
            modelNames: ["llama-local"],
            selectedModelName: "llama-local"
        )
        let updatedEndpointID = try XCTUnwrap(updatedConfiguration.selectedEndpointID)
        let didSave = await store.save(
            updatedConfiguration,
            apiKeys: [updatedEndpointID: "updated-key"]
        )
        XCTAssertTrue(didSave)

        let updatedResponse = try await provider.generateAssistantResponse(
            to: "Use updated settings",
            history: []
        )
        XCTAssertEqual(updatedResponse?.modelProvider, "openai_compatible")
        XCTAssertEqual(updatedResponse?.modelName, "llama-local")
        XCTAssertEqual(
            LocalLLMTestURLProtocol.capturedRequest?.value(forHTTPHeaderField: "Authorization"),
            "Bearer updated-key"
        )
    }

    func testReportsUnavailableConfigurationStates() async {
        let disabledProvider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        let settingsOffProvider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        let invalidEndpointProvider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(
                configuration: makeLocalLLMConfiguration(endpoint: "not a url")
            ),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        let missingModelProvider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(
                configuration: makeLocalLLMConfiguration(selectedModelName: " ")
            ),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )

        XCTAssertEqual(disabledProvider.status.detail, .message(.nativeSwiftChatLocalModelsUnavailablePlatform))
        XCTAssertEqual(settingsOffProvider.status.detail, .message(.nativeSwiftChatLocalModelSettingsOff))
        XCTAssertEqual(invalidEndpointProvider.status.detail, .message(.nativeSwiftChatEnterLocalResponsesEndpoint))
        XCTAssertEqual(missingModelProvider.status.detail, .message(.nativeSwiftChatChooseLocalModel))
    }

    func testTitleGenerationSkipsDisabledLiveLocalSettings() async throws {
        LocalLLMTestURLProtocol.reset()
        let provider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(
                configuration: makeLocalLLMConfiguration(isEnabled: false)
            ),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )

        let title = try await provider.generateTitle(from: [
            .init(id: "1", role: .user, content: "Name this", isStreaming: false)
        ])

        XCTAssertNil(title)
        XCTAssertNil(LocalLLMTestURLProtocol.capturedRequest)

        LocalLLMTestURLProtocol.reset()
        let platformDisabledProvider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(
                configuration: makeLocalLLMConfiguration()
            ),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )

        let platformDisabledTitle = try await platformDisabledProvider.generateTitle(from: [
            .init(id: "1", role: .user, content: "Name this", isStreaming: false)
        ])

        XCTAssertNil(platformDisabledTitle)
        XCTAssertNil(LocalLLMTestURLProtocol.capturedRequest)
    }
}
