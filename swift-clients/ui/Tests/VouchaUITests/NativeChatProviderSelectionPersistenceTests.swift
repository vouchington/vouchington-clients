import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatProviderSelectionPersistenceTests: XCTestCase {
    func testRapidProviderSelectionsPersistOnlyTheLatestSelection() async {
        let recorder = ProviderSelectionRecorder()
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: RecordingProviderSelectionResolver(recorder: recorder)
        )

        viewModel.selectTitleProvider(.openAI)
        await recorder.waitForFirstPersistenceStart()
        viewModel.selectTitleProvider(.anthropic)
        viewModel.selectTitleProvider(.appleFoundationModels)
        await recorder.releaseFirstPersistence()

        let selections = await recorder.waitForPersistedSelectionCount(2)

        XCTAssertEqual(selections, [.openAI, .appleFoundationModels])
    }

    func testSelectionRollsBackWhenPersistenceFails() async {
        let viewModel = NativeChatViewModel(
            client: nil,
            routeMatch: nil,
            titleProviderResolver: FailingProviderSelectionResolver()
        )

        viewModel.selectTitleProvider(.anthropic)
        await viewModel.titleProviderPersistenceTask?.value

        XCTAssertEqual(viewModel.titleProviderSelection, .openAI)
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

        viewModel?.selectTitleProvider(.anthropic)
        viewModel = nil
        await recorder.waitForFirstPersistenceStart()
        await recorder.releaseFirstPersistence()

        let selections = await recorder.waitForPersistedSelectionCount(1)
        XCTAssertEqual(selections, [.anthropic])
    }
}

private struct RecordingProviderSelectionResolver: NativeChatTitleProviderResolving {
    let recorder: ProviderSelectionRecorder

    func defaultSelection() -> NativeChatTitleProviderKind {
        .openAI
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        NativeChatHostedTitleProvider(kind: kind)
    }

    func persistSelection(_ kind: NativeChatTitleProviderKind) async -> Bool {
        await recorder.persist(kind)
        return true
    }
}

private struct FailingProviderSelectionResolver: NativeChatTitleProviderResolving {
    func defaultSelection() -> NativeChatTitleProviderKind {
        .openAI
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        NativeChatHostedTitleProvider(kind: kind)
    }

    func persistSelection(_: NativeChatTitleProviderKind) async -> Bool {
        false
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
