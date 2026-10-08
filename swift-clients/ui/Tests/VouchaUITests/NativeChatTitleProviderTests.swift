import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatTitleProviderTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testProviderSelectionDefaultsToLocalWhenFoundationModelsAreAvailable() {
        XCTAssertEqual(NativeChatTitleProviderKind.appleFoundationModels.id, "apple_foundation")
        XCTAssertEqual(NativeChatTitleProviderKind.appleFoundationModels.displayName, .message(.nativeSwiftChatLocal))
        XCTAssertEqual(
            NativeChatTitleProviderKind.appleFoundationModels.detailText,
            UiMessage(.nativeSwiftChatLocal)
        )
        XCTAssertEqual(
            NativeChatTitleProviderKind(persistedID: "missing-provider"),
            .unavailable(id: "missing-provider")
        )
    }

    func testUnavailableProviderDoesNotGenerateLocally() async throws {
        let provider = NativeChatUnavailableTitleProvider(id: "missing-provider")

        XCTAssertFalse(provider.status.isAvailable)
        let response = try await provider.generateAssistantResponse(to: "Hello", history: [])
        let title = try await provider.generateTitle(from: [])
        XCTAssertNil(response)
        XCTAssertNil(title)
    }

    func testDefaultResolverReturnsUnavailableLocalProviderWithoutHostedChoices() {
        let resolver = NativeChatLiveTitleProviderResolver(
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"])
        )
        let localProvider = resolver.provider(for: .appleFoundationModels)
        XCTAssertFalse(localProvider.status.isAvailable)
        XCTAssertNotNil(localProvider.status.detail)
        XCTAssertEqual(resolver.providerDescriptors().map(\.selection), [.appleFoundationModels])
    }

    func testResolverDefaultsToUnavailableLocalWhenConfiguredEndpointIsUnavailable() async {
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpointID: endpointID)
        )
        let resolver = NativeChatLiveTitleProviderResolver(
            settingsStore: store,
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"])
        )

        XCTAssertEqual(resolver.defaultSelection(), .appleFoundationModels)

        let emptyResolver = await NativeChatLiveTitleProviderResolver(
            settingsStore: makeLocalLLMSettingsStore(),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"])
        )
        XCTAssertEqual(emptyResolver.defaultSelection(), .appleFoundationModels)
    }

    func testPersistedUnknownProviderRemainsAnUnavailableSelection() {
        let provider = NativeChatTitleProviderKind(persistedID: "custom-provider")

        XCTAssertEqual(provider, .unavailable(id: "custom-provider"))
        XCTAssertEqual(provider.id, "custom-provider")
    }

    func testResolverUsesEndpointDisplayNameAndURLFallbackInDescriptors() async {
        let namedEndpointID = UUID()
        let unnamedEndpointID = UUID()
        let configuration = LocalLLMConfiguration(
            endpoints: [
                LocalLLMEndpointProfile(
                    id: namedEndpointID,
                    displayName: " Office model ",
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                ),
                LocalLLMEndpointProfile(
                    id: unnamedEndpointID,
                    isEnabled: true,
                    endpoint: "https://models.example.test/v1",
                    modelNames: ["hosted-local"],
                    selectedModelName: "hosted-local"
                )
            ],
            selectedEndpointID: namedEndpointID
        )
        let resolver = await NativeChatLiveTitleProviderResolver(
            settingsStore: makeLocalLLMSettingsStore(configuration: configuration),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"])
        )
        let descriptors = resolver.providerDescriptors()

        XCTAssertEqual(
            descriptors.first { $0.selection == .openAICompatible(endpointID: namedEndpointID) }?.displayName,
            .verbatim("Office model")
        )
        XCTAssertEqual(
            descriptors.first { $0.selection == .openAICompatible(endpointID: unnamedEndpointID) }?.displayName,
            .verbatim("https://models.example.test/v1")
        )
    }

    func testViewModelDefaultsTitleProviderSelectionFromResolver() {
        let provider = StubTitleProvider(
            kind: .appleFoundationModels,
            status: .init(isAvailable: true, detail: .verbatim("Local stub")),
            title: nil
        )
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )

        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, titleProviderResolver: resolver)

        XCTAssertEqual(viewModel.titleProviderSelection, .appleFoundationModels)
        XCTAssertEqual(
            viewModel.titleProviderResolver.provider(for: .appleFoundationModels).status.detail,
            .verbatim("Local stub")
        )
    }

    func testViewModelUsesLocalProviderBeforeBackendFallback() async {
        let provider = StubTitleProvider(kind: .appleFoundationModels, title: "Local title")
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z")]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")

        XCTAssertEqual(provider.generateTitleCallCount, 1)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.conversationTitleDraft, "Local title")
        XCTAssertEqual(viewModel.conversations.first?.title, "Local title")
    }

    func testViewModelSkipsLocalTitleProviderAfterNavigation() async {
        let provider = StubTitleProvider(kind: .appleFoundationModels, title: "Wrong title")
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [
            makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z"),
            makeConversation(id: "conversation-2", title: "", updatedAt: "2026-01-01T00:00:01Z")
        ]
        viewModel.createdConversationIds.insert("conversation-1")
        viewModel.selectedConversationId = "conversation-2"
        viewModel.messages = [
            .init(id: "message-2", role: .user, content: "Other conversation", isStreaming: false)
        ]

        await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")

        XCTAssertEqual(provider.generateTitleCallCount, 0)
        XCTAssertEqual(viewModel.conversations.map(\.title), ["", ""])
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testViewModelPersistsLocalGeneratedTitleWithRenameEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1"] = (
            Self.generatedConversationData,
            200
        )

        let provider = StubTitleProvider(kind: .appleFoundationModels, title: "Local title")
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z")]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")

        XCTAssertEqual(provider.generateTitleCallCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/conversations/conversation-1"
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.first??.contains(#""title":"Local title""#), true)
        XCTAssertEqual(viewModel.conversationTitleDraft, "Generated chat")
        XCTAssertEqual(viewModel.conversations.first?.title, "Generated chat")
    }

    func testViewModelRepairsLocalGeneratedTitleWhenManualRenameWinsRace() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/conversations/conversation-1"] = [
            (Self.generatedConversationData, 200, 0.05),
            (Self.manualConversationData, 200, 0)
        ]

        let provider = StubTitleProvider(kind: .appleFoundationModels, title: "Local title")
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(id: "conversation-1", title: "", updatedAt: "2026-01-01T00:00:00Z")]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        let titleTask = Task { @MainActor in
            await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")
        }
        await waitForCapturedRequestCount(1)
        viewModel.conversationTitleDraft = "Manual title"
        viewModel.updateConversation(id: "conversation-1") { $0.title = "Manual title" }
        await titleTask.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH", "PATCH"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies[0]?.contains(#""title":"Local title""#), true)
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""title":"Manual title""#), true)
        XCTAssertEqual(viewModel.conversationTitleDraft, "Manual title")
        XCTAssertEqual(viewModel.conversations.first?.title, "Manual title")
    }

    func testViewModelRevertsLocalGeneratedTitleWhenRenameFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations/conversation-1"] = (
            Self.errorData,
            500
        )

        let provider = StubTitleProvider(kind: .appleFoundationModels, title: "Local title")
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(
            id: "conversation-1",
            title: "Original",
            updatedAt: "2026-01-01T00:00:00Z"
        )]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.conversationTitleDraft = ""
        viewModel.messages = [
            .init(id: "message-1", role: .user, content: "Need a title", isStreaming: false)
        ]

        await viewModel.generateTitleIfNeeded(conversationId: "conversation-1")

        XCTAssertEqual(provider.generateTitleCallCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/conversations/conversation-1"
        ])
        XCTAssertEqual(viewModel.conversationTitleDraft, "Original")
        XCTAssertEqual(viewModel.conversations.first?.title, "Original")
    }

    func testViewModelPersistsLocalAssistantResponse() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/conversations/conversation-1/client-generated-chat"] = (
            Self.clientGeneratedChatData,
            200
        )

        let provider = StubTitleProvider(
            kind: .appleFoundationModels,
            assistantResponse: "Local assistant answer",
            title: nil,
            clientGeneratedModelName: "stale-provider-model",
            generatedModelName: "test-local-model"
        )
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(
            id: "conversation-1",
            title: "Rewards",
            updatedAt: "2026-01-01T00:00:00Z"
        )]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.loadedConversationDetailId = "conversation-1"
        viewModel.draftMessage = "How should I redeem points?"

        await viewModel.sendDraftMessage()

        XCTAssertEqual(provider.generateAssistantResponseCallCount, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/conversations/conversation-1/client-generated-chat"
        ])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.first??.contains(#""model_provider":"apple_foundation""#),
            true
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.first??.contains(#""model_name":"test-local-model""#),
            true
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.first??.contains(#""model_name":"stale-provider-model""#),
            false
        )
        XCTAssertEqual(viewModel.messages.map(\.id), ["persisted-user-1", "persisted-assistant-1"])
        XCTAssertEqual(viewModel.messages.map(\.content), [
            "How should I redeem points?",
            "Local assistant answer"
        ])
        XCTAssertFalse(viewModel.isStreaming)
    }

    func testResolverPreservesStoredEndpointSelectionAndPersistsProviderSelection() async {
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpointID: endpointID)
        )
        let selectedID = "openai_compatible:\(endpointID.uuidString.lowercased())"
        var configuration = store.load()
        configuration.selectedProviderID = selectedID
        let didSaveEndpointSelection = await store.save(configuration)
        XCTAssertTrue(didSaveEndpointSelection)

        let resolver = NativeChatLiveTitleProviderResolver(
            settingsStore: store,
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )

        XCTAssertEqual(resolver.defaultSelection(), .openAICompatible(endpointID: endpointID))
        XCTAssertEqual(
            resolver.provider(for: .openAICompatible(endpointID: endpointID)).status,
            .init(isAvailable: true, detail: .verbatim("gpt-oss"))
        )

        let didPersist = await resolver.persistSelection(.appleFoundationModels)
        XCTAssertTrue(didPersist)
        XCTAssertEqual(store.load().selectedProviderID, "apple_foundation")
    }

    func testResolverPreservesUnavailableDeletedEndpointSelectionWithoutFallback() async {
        let missingEndpointID = UUID()
        let selectedID = "openai_compatible:\(missingEndpointID.uuidString.lowercased())"
        let store = await makeLocalLLMSettingsStore()
        var configuration = store.load()
        configuration.selectedProviderID = selectedID
        let didSaveMissingSelection = await store.save(configuration)
        XCTAssertTrue(didSaveMissingSelection)

        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)
        XCTAssertEqual(resolver.defaultSelection(), .openAICompatible(endpointID: missingEndpointID))
        XCTAssertFalse(resolver.provider(for: .openAICompatible(endpointID: missingEndpointID)).status.isAvailable)
        XCTAssertTrue(resolver.providerDescriptors().contains {
            $0.selection == .openAICompatible(endpointID: missingEndpointID) && $0.id == selectedID
        })
    }

    func testViewModelDoesNotSilentlyFallbackWhenLocalProviderUnavailable() async throws {
        let provider = StubTitleProvider(
            kind: .appleFoundationModels,
            status: .init(isAvailable: false, detail: .verbatim("Apple Intelligence is unavailable.")),
            title: nil
        )
        let resolver = StubTitleProviderResolver(
            defaultSelectionValue: .appleFoundationModels,
            providers: [.appleFoundationModels: provider]
        )
        let client = try makeClient()
        let viewModel = NativeChatViewModel(client: client, routeMatch: nil, titleProviderResolver: resolver)
        viewModel.conversations = [makeConversation(
            id: "conversation-1",
            title: "Rewards",
            updatedAt: "2026-01-01T00:00:00Z"
        )]
        viewModel.selectedConversationId = "conversation-1"
        viewModel.loadedConversationDetailId = "conversation-1"
        viewModel.draftMessage = "How should I redeem points?"

        await viewModel.sendDraftMessage()

        XCTAssertEqual(provider.generateAssistantResponseCallCount, 0)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.streamErrorMessage, .verbatim("Apple Intelligence is unavailable."))
        XCTAssertFalse(viewModel.isStreaming)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNil(viewModel.streamingUserMessageId)
    }

}
