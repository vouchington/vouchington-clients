import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatTitleProviderResolverReviewTests: XCTestCase {
    func testDefaultSelectionSkipsDisabledAndInvalidEndpoints() async {
        let disabledID = UUID()
        let invalidID = UUID()
        let validID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: disabledID,
                    isEnabled: false,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["disabled-model"],
                    selectedModelName: "disabled-model"
                ),
                LocalLLMEndpointProfile(
                    id: invalidID,
                    isEnabled: true,
                    endpoint: "not a URL",
                    modelNames: ["invalid-model"],
                    selectedModelName: "invalid-model"
                ),
                LocalLLMEndpointProfile(
                    id: validID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["valid-model"],
                    selectedModelName: "valid-model"
                )
            ],
            selectedEndpointID: validID
        )
        let resolver = await NativeChatLiveTitleProviderResolver(
            settingsStore: makeLocalLLMSettingsStore(configuration: configuration),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            appleProvider: unavailableAppleProvider()
        )

        XCTAssertEqual(resolver.defaultSelection(), .openAICompatible(endpointID: validID))
    }

    func testDefaultSelectionUsesHostedProviderWhenNoEndpointIsAvailable() async {
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [LocalLLMEndpointProfile(
                isEnabled: false,
                endpoint: "not a URL",
                modelNames: ["invalid-model"],
                selectedModelName: "invalid-model"
            )]
        )
        let resolver = await NativeChatLiveTitleProviderResolver(
            settingsStore: makeLocalLLMSettingsStore(configuration: configuration),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            appleProvider: unavailableAppleProvider()
        )

        XCTAssertEqual(resolver.defaultSelection(), .openAI)
    }

    func testPersistSelectionSynchronizesOpenAICompatibleEndpointSelection() async {
        let firstID = UUID()
        let secondID = UUID()
        let configuration = LocalLLMConfiguration(
            endpoints: [
                LocalLLMEndpointProfile(id: firstID, endpoint: "http://localhost:2999/v1"),
                LocalLLMEndpointProfile(id: secondID, endpoint: "http://localhost:3000/v1")
            ],
            selectedEndpointID: firstID
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)

        let didPersist = await resolver.persistSelection(.openAICompatible(endpointID: secondID))
        XCTAssertTrue(didPersist)

        let persisted = store.load()
        XCTAssertEqual(persisted.selectedEndpointID, secondID)
        XCTAssertEqual(
            persisted.selectedProviderID,
            "openai_compatible:\(secondID.uuidString.lowercased())"
        )
    }

    private func unavailableAppleProvider() -> StubTitleProvider {
        StubTitleProvider(
            kind: .appleFoundationModels,
            status: .init(isAvailable: false, detail: nil),
            title: nil
        )
    }
}
