import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeChatAndroidAICoreProviderTests: XCTestCase {
    func testKindIdentityRoundTripsCanonicalAndLegacyPersistedIDs() {
        XCTAssertEqual(NativeChatTitleProviderKind.androidAICore.id, "android_aicore")
        XCTAssertEqual(NativeChatTitleProviderKind(persistedID: "android_aicore"), .androidAICore)
        XCTAssertEqual(NativeChatTitleProviderKind(persistedID: "aiCore"), .androidAICore)
        XCTAssertTrue(NativeChatTitleProviderKind.androidAICore.isLocal)
        XCTAssertNil(NativeChatTitleProviderKind.androidAICore.hostedProviderValue)
        XCTAssertEqual(
            NativeChatTitleProviderKind.androidAICore.displayName,
            .message(.nativeSwiftAndroidProviderAicore)
        )
        XCTAssertEqual(
            NativeChatTitleProviderKind.androidAICore.detailText,
            UiMessage(.nativeSwiftChatLocalModelsUnavailablePlatform)
        )
    }

    func testProviderAlwaysResolvesToTheUnavailableStateOnThisPlatform() async throws {
        let provider = NativeChatAndroidAICoreProvider()

        XCTAssertEqual(
            provider.status,
            .init(
                isAvailable: false,
                detail: .message(
                    .nativeSwiftRouteSurfaceProviderUnavailable,
                    parameters: ["provider": "android_aicore"]
                )
            )
        )
        let response = try await provider.generateAssistantResponse(to: "Hi", history: [])
        let title = try await provider.generateTitle(from: [])
        XCTAssertNil(response)
        XCTAssertNil(title)
    }

    func testResolverRoutesAndroidAICoreToTheUnavailableStateAndExcludesItFromTheDefaultPicker() async {
        let store = await makeLocalLLMSettingsStore()
        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)

        let provider = resolver.provider(for: .androidAICore)
        XCTAssertFalse(provider.status.isAvailable)
        XCTAssertEqual(
            provider.status.detail,
            .message(.nativeSwiftRouteSurfaceProviderUnavailable, parameters: ["provider": "android_aicore"])
        )
        XCTAssertFalse(resolver.providerDescriptors().contains { $0.selection == .androidAICore })
    }

    func testResolverSurfacesAPersistedAndroidAICoreSelectionThroughTheDescriptorFallback() async {
        let store = await makeLocalLLMSettingsStore()
        var configuration = store.load()
        configuration.selectedProviderID = "android_aicore"
        let didSave = await store.save(configuration)
        XCTAssertTrue(didSave)

        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)
        XCTAssertEqual(resolver.defaultSelection(), .androidAICore)
        XCTAssertTrue(resolver.providerDescriptors().contains {
            $0.selection == .androidAICore && $0.id == "android_aicore"
        })
    }

    func testResolverSurfacesALegacyAiCorePersistedSelectionAsAndroidAICore() async {
        let store = await makeLocalLLMSettingsStore()
        var configuration = store.load()
        configuration.selectedProviderID = "aiCore"
        let didSave = await store.save(configuration)
        XCTAssertTrue(didSave)

        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)
        XCTAssertEqual(resolver.defaultSelection(), .androidAICore)
        XCTAssertTrue(resolver.providerDescriptors().contains {
            $0.selection == .androidAICore && $0.id == "android_aicore"
        })
    }

    func testResolverPersistsAnAndroidAICoreSelection() async {
        let store = await makeLocalLLMSettingsStore()
        let resolver = NativeChatLiveTitleProviderResolver(settingsStore: store)

        let didPersist = await resolver.persistSelection(.androidAICore)

        XCTAssertTrue(didPersist)
        XCTAssertEqual(store.load().selectedProviderID, "android_aicore")
    }
}
