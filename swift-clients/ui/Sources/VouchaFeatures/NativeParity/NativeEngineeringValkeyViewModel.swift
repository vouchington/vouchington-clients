import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization

enum NativeEngineeringValkeyPendingAction: Hashable {
    case clearGroup(String)
    case clearAll
}

@Observable
@MainActor
final class NativeEngineeringValkeyViewModel: NativeEngineeringActionPerforming {
    var state: LoadState = .idle
    var cacheGroups: [EngineeringValkeyCacheGroup] = []
    var pendingAction: NativeEngineeringValkeyPendingAction?
    var actionErrorMessage: UiVerbatimText?

    private let client: APIClient
    var actionLoadingKeys: Set<String> = []

    init(client: APIClient) {
        self.client = client
    }

    func load() async {
        if case .loading = state {
            return
        }
        state = .loading
        do {
            let response: EngineeringValkeyCacheGroupsResponse = try await client.send(.valkeyCacheGroups)
            cacheGroups = response.groups
            actionErrorMessage = nil
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func isActionLoading(_ key: String) -> Bool {
        actionLoadingKeys.contains(key)
    }

    func rebuild(filter: EngineeringValkeyBloomFilterTarget) async {
        await performAction(key: "rebuild|\(filter.rawValue)") {
            let _: EngineeringValkeyRebuildResponse = try await self.client
                .send(.valkeyRebuildBloomFilter(filter: filter))
        }
    }

    func clearCache(group: String) async {
        await performAction(key: "clear|\(group)") {
            let _: EngineeringValkeyClearCacheResponse = try await self.client.send(.valkeyClearCache(group: group))
        }
    }

    func flush(concern: EngineeringValkeyFlushConcern) async {
        await performAction(key: "flush|\(concern.rawValue)") {
            let _: EngineeringValkeyFlushResponse = try await self.client
                .send(.valkeyFlush(concern: concern, force: concern.requiresForce ? true : nil))
        }
    }

    func confirmPendingAction() async {
        guard let pendingAction else { return }
        self.pendingAction = nil
        switch pendingAction {
        case let .clearGroup(group):
            await clearCache(group: group)
        case .clearAll:
            await clearCache(group: "all")
        }
    }
}
