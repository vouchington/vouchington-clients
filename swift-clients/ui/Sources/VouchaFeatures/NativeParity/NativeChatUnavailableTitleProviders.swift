import VouchaLocalization

struct NativeChatUnavailableTitleProvider: NativeChatTitleProviding {
    let id: String

    var status: NativeChatTitleProviderStatus {
        .init(
            isAvailable: false,
            detail: .message(
                .nativeSwiftRouteSurfaceProviderUnavailable,
                parameters: ["provider": id]
            )
        )
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        nil
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        nil
    }
}
