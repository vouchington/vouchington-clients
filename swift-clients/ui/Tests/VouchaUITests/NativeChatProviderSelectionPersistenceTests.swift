import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatProviderSelectionPersistenceTests: XCTestCase {
    func testRapidProviderSelectionsPersistOnlyTheLatestSelection() async {
        let recorder = ProviderSelectionRecorder()
        let endpointID = UUID()
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: RecordingProviderSelectionResolver(recorder: recorder)
        )

        viewModel.selectTitleProvider(.androidAICore)
        await recorder.waitForFirstPersistenceStart()
        viewModel.selectTitleProvider(.appleFoundationModels)
        viewModel.selectTitleProvider(.openAICompatible(endpointID: endpointID))
        await recorder.releaseFirstPersistence()

        let selections = await recorder.waitForPersistedSelectionCount(2)

        XCTAssertEqual(selections, [.androidAICore, .openAICompatible(endpointID: endpointID)])
    }

    func testSelectionRollsBackWhenPersistenceFails() async {
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: FailingProviderSelectionResolver()
        )

        viewModel.selectTitleProvider(.androidAICore)
        await viewModel.titleProviderPersistenceTask?.value

        XCTAssertEqual(viewModel.titleProviderSelection, .appleFoundationModels)
    }

    func testFailedSelectionPersistenceDoesNotReplaceAnActiveTurnProvider() async {
        let recorder = ProviderSelectionRecorder()
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: FailingProviderSelectionResolver(recorder: recorder)
        )
        viewModel.selectedConversationId = "conversation-1"
        viewModel.selectTitleProvider(.androidAICore)
        await recorder.waitForFirstPersistenceStart()
        viewModel.pendingLocalTurn = .init(
            context: .init(
                conversationId: "conversation-1", text: "Hello", userMessageId: "user-1",
                assistantMessageId: "assistant-1", createdConversation: false,
                providerSelection: .androidAICore
            ),
            response: .init(content: "Reply", modelProvider: "android_aicore", modelName: nil)
        )
        viewModel.isSendingDraft = true
        await recorder.releaseFirstPersistence()
        await viewModel.titleProviderPersistenceTask?.value

        XCTAssertEqual(viewModel.titleProviderSelection, .androidAICore)
        XCTAssertEqual(viewModel.pendingLocalTurn?.context.userMessageId, "user-1")

        viewModel.isSendingDraft = false
        XCTAssertEqual(viewModel.titleProviderSelection, .androidAICore)
        viewModel.pendingLocalTurn = nil
        XCTAssertEqual(viewModel.titleProviderSelection, .appleFoundationModels)
    }

    func testSelectingAndroidAICorePersistsItsPersistedIDThroughTheResolver() async {
        let recorder = ProviderSelectionRecorder()
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: RecordingProviderSelectionResolver(recorder: recorder)
        )

        viewModel.selectTitleProvider(.androidAICore)
        await recorder.waitForFirstPersistenceStart()
        await recorder.releaseFirstPersistence()

        let selections = await recorder.waitForPersistedSelectionCount(1)

        XCTAssertEqual(selections, [.androidAICore])
        XCTAssertEqual(selections.first?.id, "android_aicore")
        XCTAssertEqual(NativeChatTitleProviderKind(persistedID: "android_aicore"), .androidAICore)
    }

    func testLatestSelectionPersistsAfterViewModelIsReleased() async {
        let recorder = ProviderSelectionRecorder()
        var viewModel: NativeChatViewModel? = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: RecordingProviderSelectionResolver(recorder: recorder)
        )

        viewModel?.selectTitleProvider(.androidAICore)
        viewModel = nil
        await recorder.waitForFirstPersistenceStart()
        await recorder.releaseFirstPersistence()

        let selections = await recorder.waitForPersistedSelectionCount(1)
        XCTAssertEqual(selections, [.androidAICore])
    }
}

private struct RecordingProviderSelectionResolver: NativeChatTitleProviderResolving {
    let recorder: ProviderSelectionRecorder

    func defaultSelection() -> NativeChatTitleProviderKind {
        .appleFoundationModels
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        NativeChatUnavailableTitleProvider(id: kind.id)
    }

    func persistSelection(_ kind: NativeChatTitleProviderKind) async -> Bool {
        await recorder.persist(kind)
        return true
    }
}

private struct FailingProviderSelectionResolver: NativeChatTitleProviderResolving {
    let recorder: ProviderSelectionRecorder?

    init(recorder: ProviderSelectionRecorder? = nil) {
        self.recorder = recorder
    }

    func defaultSelection() -> NativeChatTitleProviderKind {
        .appleFoundationModels
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        NativeChatUnavailableTitleProvider(id: kind.id)
    }

    func persistSelection(_ kind: NativeChatTitleProviderKind) async -> Bool {
        if let recorder { await recorder.persist(kind) }
        return false
    }
}

private actor ProviderSelectionRecorder {
    private var persistedSelections: [NativeChatTitleProviderKind] = []
    private var didStartFirstPersistence = false
    private var firstPersistenceStartWaiters: [CheckedContinuation<Void, Never>] = []
    private var firstPersistenceRelease: CheckedContinuation<Void, Never>?

    func persist(_ selection: NativeChatTitleProviderKind) async {
        if !didStartFirstPersistence {
            didStartFirstPersistence = true
            firstPersistenceStartWaiters.forEach { $0.resume() }
            firstPersistenceStartWaiters = []
            await withCheckedContinuation { firstPersistenceRelease = $0 }
        }
        persistedSelections.append(selection)
    }

    func waitForFirstPersistenceStart() async {
        guard !didStartFirstPersistence else { return }
        await withCheckedContinuation { firstPersistenceStartWaiters.append($0) }
    }

    func releaseFirstPersistence() {
        firstPersistenceRelease?.resume()
        firstPersistenceRelease = nil
    }

    func waitForPersistedSelectionCount(_ count: Int) async -> [NativeChatTitleProviderKind] {
        while persistedSelections.count < count {
            await Task.yield()
        }
        return persistedSelections
    }
}
