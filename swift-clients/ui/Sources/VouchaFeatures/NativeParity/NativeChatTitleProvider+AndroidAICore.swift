/// Android's system AICore is not reachable from `ui/`'s `VouchaFeatures` package: the on-device
/// bridge (`AndroidAICoreRunning`) lives only in the standalone Skip Fuse `apps/android` shell,
/// which depends on `VouchaCore`/`VouchaLocalization` but not `VouchaFeatures`, and `ui/` itself
/// has no Skip Fuse dependency to reach it through. There is no compile-time signal in this
/// package (`#if os(Android)` would never be true — `ui/` does not build for Android at all today)
/// that would let this conformer do anything other than report itself unavailable, so it always
/// delegates to `NativeChatUnavailableTitleProvider`, matching what
/// `NativeChatTitleProvider+Resolver.swift` documents as the resolved behavior for
/// `.androidAICore` on every platform this package builds for.
///
/// Until #6745 lands real Android reachability into this shared picker, this type exists purely
/// to give `.androidAICore` a dedicated, testable provider identity to grow into. When a real
/// bridge becomes reachable, generation should send the identity the backend already accepts:
/// `model_provider: "android_aicore"`, `model_name: "android-aicore-system"`
/// (see `backend/services/agents/model-providers.mts`).
struct NativeChatAndroidAICoreProvider: NativeChatTitleProviding {
    private let unavailableProvider: NativeChatUnavailableTitleProvider

    init() {
        unavailableProvider = NativeChatUnavailableTitleProvider(id: NativeChatTitleProviderKind.androidAICore.id)
    }

    var status: NativeChatTitleProviderStatus {
        unavailableProvider.status
    }

    func generateAssistantResponse(
        to message: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        try await unavailableProvider.generateAssistantResponse(to: message, history: history)
    }

    func generateTitle(from messages: [NativeChatTimelineMessage]) async throws -> String? {
        try await unavailableProvider.generateTitle(from: messages)
    }
}
