import VouchaCore
@testable import VouchaFeatures
import XCTest

final class LocalLLMEndpointDraftSyncTests: XCTestCase {
    func testResolveClearsDraftAndSecretStateWhenOriginChanges() {
        let id = UUID()
        let oldProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let newProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:3000/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let syncedDraft = LocalLLMEndpointDraft(profile: oldProfile)
        var currentDraft = syncedDraft
        currentDraft.displayName = "In-flight edit"
        currentDraft.apiKey = "typed-key"

        let resolution = LocalLLMEndpointDraftSync.resolve(
            oldProfile: oldProfile, newProfile: newProfile, currentDraft: currentDraft, syncedDraft: syncedDraft
        )

        XCTAssertTrue(resolution.didClearSecretState)
        XCTAssertEqual(
            resolution.draft.apiKey,
            "",
            "an origin change must clear the API key, even if the user had typed one"
        )
        XCTAssertEqual(
            resolution.draft.displayName,
            "Model",
            "the fresh server value replaces any in-flight edit on a security-relevant change"
        )
        XCTAssertEqual(resolution.syncedDraft, resolution.draft)
    }

    func testResolvePreservesInFlightEditWhenDraftHasDivergedSinceLastSubmit() {
        let id = UUID()
        let profile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let syncedDraft = LocalLLMEndpointDraft(profile: profile)
        var currentDraft = syncedDraft
        currentDraft.displayName = "Edited after submit"

        let resolution = LocalLLMEndpointDraftSync.resolve(
            oldProfile: profile, newProfile: profile, currentDraft: currentDraft, syncedDraft: syncedDraft
        )

        XCTAssertFalse(resolution.didClearSecretState)
        XCTAssertEqual(
            resolution.draft,
            currentDraft,
            "an edit made after the last submit must not be clobbered by the reload"
        )
        XCTAssertEqual(resolution.syncedDraft, syncedDraft)
    }

    func testResolveResyncsToServerNormalizedValuesWhenDraftMatchesLastSubmit() {
        let id = UUID()
        let oldProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: [], selectedModelName: ""
        )
        let newProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        var syncedDraft = LocalLLMEndpointDraft(profile: oldProfile)
        syncedDraft.modelsText = "gpt-oss\n"
        syncedDraft.apiKey = "typed-key"
        let currentDraft = syncedDraft

        let resolution = LocalLLMEndpointDraftSync.resolve(
            oldProfile: oldProfile, newProfile: newProfile, currentDraft: currentDraft, syncedDraft: syncedDraft
        )

        XCTAssertFalse(resolution.didClearSecretState)
        XCTAssertEqual(
            resolution.draft.modelsText,
            "gpt-oss",
            "a successful reload must resync to the server-normalized value, not keep the user's raw submitted text"
        )
        XCTAssertEqual(resolution.draft.apiKey, "typed-key", "the in-flight API key must survive a same-secret reload")
        XCTAssertEqual(resolution.syncedDraft, resolution.draft)
    }

    func testResolveTreatsIDChangeAsNotSameSecret() {
        let oldProfile = LocalLLMEndpointProfile(
            id: UUID(), displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let newProfile = LocalLLMEndpointProfile(
            id: UUID(), displayName: "Other", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let syncedDraft = LocalLLMEndpointDraft(profile: oldProfile)

        let resolution = LocalLLMEndpointDraftSync.resolve(
            oldProfile: oldProfile, newProfile: newProfile, currentDraft: syncedDraft, syncedDraft: syncedDraft
        )

        XCTAssertTrue(resolution.didClearSecretState)
        XCTAssertEqual(resolution.draft.displayName, "Other")
    }

    func testCredentialLoadIdentityChangesWhenOriginChangesOnTheSameProfileID() {
        let id = UUID()
        let oldProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )
        let newProfile = LocalLLMEndpointProfile(
            id: id, displayName: "Model", isEnabled: true,
            endpoint: "http://localhost:3000/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        )

        XCTAssertNotEqual(
            oldProfile.credentialLoadIdentity,
            newProfile.credentialLoadIdentity,
            "a same-id origin change must restart .task(id:) so a stale Keychain read cannot restore the old host's secret"
        )
        XCTAssertEqual(oldProfile.credentialLoadIdentity, LocalLLMEndpointProfile(
            id: id, displayName: "Renamed", isEnabled: true,
            endpoint: "http://localhost:2999/v1", modelNames: ["gpt-oss"], selectedModelName: "gpt-oss"
        ).credentialLoadIdentity)
    }
}
