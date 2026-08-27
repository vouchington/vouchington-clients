import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsLocalLLMEndpointEditorInteractionTests: NativeRouteSurfaceViewModelTestCase {
    func testTaskLoadSetsDraftAndBaselineWithoutMarkingTheAPIKeyEdited() async throws {
        let endpointID = UUID()
        let profile = makeEditorProfile(id: endpointID)
        let secrets = RecordingLocalLLMSecretStore(apiKeys: [endpointID: "stored-key"])
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        let (_, sut) = try await makeEditor(
            profile: profile,
            secrets: secrets,
            interactionState: interactionState
        )

        try await ViewHosting.host(sut) {
            try await waitUntil { interactionState.loadedAPIKey == "stored-key" }
            XCTAssertEqual(interactionState.draft.apiKey, "stored-key")
            XCTAssertFalse(interactionState.apiKeyWasEdited)
        }
    }

    func testUserEditAfterLoadMarksTheAPIKeyEdited() async throws {
        let endpointID = UUID()
        let profile = makeEditorProfile(id: endpointID)
        let secrets = RecordingLocalLLMSecretStore(apiKeys: [endpointID: "stored-key"])
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        let (_, sut) = try await makeEditor(
            profile: profile,
            secrets: secrets,
            interactionState: interactionState
        )

        try await ViewHosting.host(sut) {
            try await waitUntil { interactionState.loadedAPIKey == "stored-key" }
            try sut.inspect().find(ViewType.SecureField.self).setInput("edited-key")
            try sut.inspect().find(ViewType.SecureField.self)
                .callOnChange(oldValue: "stored-key", newValue: "edited-key")
            XCTAssertEqual(interactionState.draft.apiKey, "edited-key")
            XCTAssertTrue(interactionState.apiKeyWasEdited)
            XCTAssertEqual(interactionState.loadedAPIKey, "stored-key")
        }
    }

    func testProfileSwitchResetsTheLoadedBaselineSoThePreviousSecretCannotLeak() async throws {
        let firstID = UUID()
        let secondID = UUID()
        let first = makeEditorProfile(id: firstID, endpoint: "http://localhost:2999/v1")
        let second = makeEditorProfile(id: secondID, endpoint: "http://localhost:3000/v1")
        let secrets = RecordingLocalLLMSecretStore(apiKeys: [firstID: "first-key", secondID: "second-key"])
        let store = try await makeEditorStore(
            configuration: LocalLLMConfiguration(
                isEnabled: true,
                endpoints: [first, second],
                selectedEndpointID: firstID
            ),
            secrets: secrets
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: first)
        let host = LocalLLMEndpointEditorProfileHost(profile: first)
        let sut = LocalLLMEndpointEditorHost(
            host: host,
            isSelected: true,
            viewModel: viewModel,
            interactionState: interactionState
        )

        try await ViewHosting.host(sut) {
            try await waitUntil { interactionState.loadedAPIKey == "first-key" }
            try sut.inspect().find(ViewType.SecureField.self).setInput("typed-for-first")
            try sut.inspect().find(ViewType.SecureField.self)
                .callOnChange(oldValue: "first-key", newValue: "typed-for-first")
            XCTAssertTrue(interactionState.apiKeyWasEdited)

            host.profile = second
            try await waitUntil {
                interactionState.loadedAPIKey == "second-key" && interactionState.draft.apiKey == "second-key"
            }
            XCTAssertFalse(interactionState.apiKeyWasEdited)
        }
    }

    func testSuccessfulSaveUpdatesTheBaselineSoReenteringTheSavedValueIsNotAnEdit() async throws {
        let endpointID = UUID()
        let profile = makeEditorProfile(id: endpointID)
        let secrets = RecordingLocalLLMSecretStore(apiKeys: [endpointID: "stored-key"])
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        let (_, sut) = try await makeEditor(
            profile: profile,
            secrets: secrets,
            interactionState: interactionState
        )

        try await ViewHosting.host(sut) {
            try await waitUntil { interactionState.loadedAPIKey == "stored-key" }
            try sut.inspect().find(ViewType.SecureField.self).setInput("saved-key")
            try sut.inspect().find(ViewType.SecureField.self)
                .callOnChange(oldValue: "stored-key", newValue: "saved-key")
            XCTAssertTrue(interactionState.apiKeyWasEdited)

            try sut.inspect().find(button: uiEnglish(.nativeSwiftCommonSave)).tap()
            try await waitUntil { interactionState.loadedAPIKey == "saved-key" && !interactionState.apiKeyWasEdited }

            try sut.inspect().find(ViewType.SecureField.self).setInput("other-key")
            try sut.inspect().find(ViewType.SecureField.self)
                .callOnChange(oldValue: "saved-key", newValue: "other-key")
            XCTAssertTrue(interactionState.apiKeyWasEdited)
            try sut.inspect().find(ViewType.SecureField.self).setInput("saved-key")
            try sut.inspect().find(ViewType.SecureField.self)
                .callOnChange(oldValue: "other-key", newValue: "saved-key")
            XCTAssertFalse(interactionState.apiKeyWasEdited)
            XCTAssertEqual(interactionState.loadedAPIKey, "saved-key")
        }
    }

    func testApplyLoadedAPIKeyIgnoresACancelledLoad() {
        let profile = makeEditorProfile(id: UUID())
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)

        interactionState.applyLoadedAPIKey("stored-key", isCancelled: true)

        XCTAssertEqual(interactionState.loadedAPIKey, "")
        XCTAssertEqual(interactionState.draft.apiKey, "")
        XCTAssertFalse(interactionState.apiKeyWasEdited)
    }

    func testApplySuccessfulSaveLeavesTheBaselineWhenTheDraftDiverged() {
        let profile = makeEditorProfile(id: UUID())
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        interactionState.draft.apiKey = "saved-key"
        let submitted = interactionState.draft
        interactionState.draft.apiKey = "edited-after-submit"

        interactionState.applySuccessfulSave(submittedDraft: submitted)

        XCTAssertEqual(interactionState.draft.apiKey, "edited-after-submit")
        XCTAssertEqual(interactionState.loadedAPIKey, "")
    }

    func testApplyLoadedAPIKeyClearsEditedWhenTheDraftAlreadyMatchesTheLoadedKey() {
        let profile = makeEditorProfile(id: UUID())
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        interactionState.draft.apiKey = "stored-key"
        interactionState.apiKeyWasEdited = true

        interactionState.applyLoadedAPIKey("stored-key", isCancelled: false)

        XCTAssertEqual(interactionState.loadedAPIKey, "stored-key")
        XCTAssertEqual(interactionState.draft.apiKey, "stored-key")
        XCTAssertFalse(interactionState.apiKeyWasEdited)
    }

    func testInFlightCredentialLoadDoesNotOverwriteAUserEdit() async throws {
        let endpointID = UUID()
        let profile = makeEditorProfile(id: endpointID)
        let secrets = GatedLocalLLMSecretStore(apiKeys: [endpointID: "stored-key"])
        let interactionState = LocalLLMEndpointEditorInteractionState(profile: profile)
        let store = try await makeEditorStore(
            configuration: LocalLLMConfiguration(
                isEnabled: true,
                endpoints: [profile],
                selectedEndpointID: profile.id
            ),
            secrets: secrets
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let host = LocalLLMEndpointEditorProfileHost(profile: profile)
        let sut = LocalLLMEndpointEditorHost(
            host: host,
            isSelected: true,
            viewModel: viewModel,
            interactionState: interactionState
        )

        try await ViewHosting.host(sut) {
            try await waitUntil { await secrets.readStarted }
            do {
                try sut.inspect().find(ViewType.SecureField.self).setInput("typed-before-load")
                try sut.inspect().find(ViewType.SecureField.self)
                    .callOnChange(oldValue: "", newValue: "typed-before-load")
                XCTAssertTrue(interactionState.apiKeyWasEdited)
            } catch {
                await secrets.resumeRead()
                throw error
            }
            await secrets.resumeRead()
            try await waitUntil { interactionState.loadedAPIKey == "stored-key" }
            XCTAssertEqual(
                interactionState.draft.apiKey,
                "typed-before-load",
                "a late Keychain result must keep the typed draft when the user already edited the field"
            )
            XCTAssertTrue(interactionState.apiKeyWasEdited)
        }
    }

    private func makeEditorProfile(
        id: UUID,
        endpoint: String = "http://localhost:2999/v1"
    ) -> LocalLLMEndpointProfile {
        LocalLLMEndpointProfile(
            id: id,
            displayName: "Local",
            isEnabled: true,
            endpoint: endpoint,
            modelNames: ["gpt-oss"],
            selectedModelName: "gpt-oss"
        )
    }

    private func makeEditor(
        profile: LocalLLMEndpointProfile,
        secrets: some LocalLLMSecretStoring,
        interactionState: LocalLLMEndpointEditorInteractionState
    ) async throws -> (SettingsViewModel, LocalLLMEndpointEditorHost) {
        let store = try await makeEditorStore(
            configuration: LocalLLMConfiguration(
                isEnabled: true,
                endpoints: [profile],
                selectedEndpointID: profile.id
            ),
            secrets: secrets
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let host = LocalLLMEndpointEditorProfileHost(profile: profile)
        return (
            viewModel,
            LocalLLMEndpointEditorHost(
                host: host,
                isSelected: true,
                viewModel: viewModel,
                interactionState: interactionState
            )
        )
    }

    private func makeEditorStore(
        configuration: LocalLLMConfiguration,
        secrets: some LocalLLMSecretStoring
    ) async throws -> LocalLLMSettingsStore {
        let store = LocalLLMSettingsStore(
            fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
            secretStore: secrets
        )
        let didSave = await store.save(configuration)
        XCTAssertTrue(didSave)
        return store
    }

    private func waitUntil(
        _ condition: @escaping @MainActor () async throws -> Bool,
        timeout: Duration = .seconds(2)
    ) async throws {
        let clock = ContinuousClock()
        let deadline = clock.now.advanced(by: timeout)
        var lastError: Error?
        while true {
            do {
                if try await condition() {
                    return
                }
                lastError = nil
            } catch {
                lastError = error
            }
            guard clock.now < deadline else {
                throw LocalLLMEndpointEditorWaitTimeout(timeout: timeout, lastError: lastError)
            }
            try await clock.sleep(for: .milliseconds(10))
        }
    }
}

@Observable
@MainActor
private final class LocalLLMEndpointEditorProfileHost {
    var profile: LocalLLMEndpointProfile

    init(profile: LocalLLMEndpointProfile) {
        self.profile = profile
    }
}

private struct LocalLLMEndpointEditorHost: View {
    var host: LocalLLMEndpointEditorProfileHost
    var isSelected: Bool
    var viewModel: SettingsViewModel
    var interactionState: LocalLLMEndpointEditorInteractionState

    var body: some View {
        LocalLLMEndpointEditor(
            profile: host.profile,
            isSelected: isSelected,
            viewModel: viewModel,
            interactionState: interactionState
        )
        .environment(\.locale, Locale(identifier: "en"))
    }
}

private actor RecordingLocalLLMSecretStore: LocalLLMSecretStoring {
    private var apiKeys: [UUID: String]

    init(apiKeys: [UUID: String]) {
        self.apiKeys = apiKeys
    }

    func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async {
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async {
        apiKeys[endpointID] = nil
    }
}

private actor GatedLocalLLMSecretStore: LocalLLMSecretStoring {
    private var apiKeys: [UUID: String]
    private(set) var readStarted = false
    private var continuation: CheckedContinuation<Void, Never>?

    init(apiKeys: [UUID: String]) {
        self.apiKeys = apiKeys
    }

    func readAPIKey(for endpointID: UUID) async -> String? {
        readStarted = true
        await withCheckedContinuation { continuation = $0 }
        return apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async {
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async {
        apiKeys[endpointID] = nil
    }

    func resumeRead() {
        continuation?.resume()
        continuation = nil
    }
}

private struct LocalLLMEndpointEditorWaitTimeout: LocalizedError {
    let timeout: Duration
    let lastError: Error?

    var errorDescription: String? {
        if let lastError {
            return "Timed out after \(timeout) waiting for LocalLLMEndpointEditor state: \(lastError)"
        }
        return "Timed out after \(timeout) waiting for LocalLLMEndpointEditor state"
    }
}
