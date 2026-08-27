import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization

enum NativeEngineeringPostgresqlPendingAction: Hashable {
    case createPartitions
    case cleanupPartitions
}

@Observable
@MainActor
final class NativeEngineeringPostgresqlViewModel: NativeEngineeringActionPerforming {
    var state: LoadState = .idle
    var migrations: EngineeringMigrationsResponse?
    var partitions: EngineeringPartitionStatusResponse?
    var articleSyncJobId: String?
    var articleSyncStatus: ArticleSyncJobStatus?
    var pendingAction: NativeEngineeringPostgresqlPendingAction?
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
            async let migrationsResponse: EngineeringMigrationsResponse = client.send(.psqlMigrations)
            async let partitionsResponse: EngineeringPartitionStatusResponse = client.send(.psqlPartitions)
            migrations = try await migrationsResponse
            partitions = try await partitionsResponse
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

    func runJob(_ type: EngineeringPsqlJobType) async {
        await performAction(key: "job|\(type.rawValue)") {
            let _: EngineeringSuccessResponse = try await self.client.send(.psqlJobs(type: type))
        }
    }

    func confirmPendingAction() async {
        guard let pendingAction else { return }
        self.pendingAction = nil
        switch pendingAction {
        case .createPartitions:
            await runJob(.createPartitions)
        case .cleanupPartitions:
            await runJob(.cleanupPartitions)
        }
    }

    func triggerArticleSync() async {
        await performAction(key: "article-sync") {
            let response: ArticleSyncTriggerResponse = try await self.client.send(.articleSyncTrigger)
            self.articleSyncJobId = response.jobId
            self.articleSyncStatus = nil
            do {
                self.articleSyncStatus = try await self.client.send(.articleSyncStatus(jobId: response.jobId))
            } catch {
                self.articleSyncStatus = nil
            }
        }
    }

    func refreshArticleSyncStatus() async {
        guard let articleSyncJobId else { return }
        await performAction(key: "article-sync-status|\(articleSyncJobId)") {
            self.articleSyncStatus = try await self.client.send(.articleSyncStatus(jobId: articleSyncJobId))
        }
    }
}
