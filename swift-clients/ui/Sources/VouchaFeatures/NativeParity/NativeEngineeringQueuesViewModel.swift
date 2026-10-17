import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization

@Observable
@MainActor
final class NativeEngineeringQueuesViewModel: NativeEngineeringActionPerforming {
    var state: LoadState = .idle
    var stats: EngineeringAggregatedQueueStats?
    var queues: [EngineeringQueueStats] = []
    var domains: [EngineeringQueueDomain] = []
    var scheduledJobs: [EngineeringScheduledJob] = []
    var backfills: [EngineeringBackfill] = []
    var actionErrorMessage: UiVerbatimText?

    let client: APIClient
    var actionLoadingKeys: Set<String> = []
    private var reloadRequested = false
    private var isReloadDraining = false

    init(client: APIClient) {
        self.client = client
    }

    func load() async {
        if isReloadDraining {
            reloadRequested = true
            return
        }
        isReloadDraining = true
        defer {
            isReloadDraining = false
            reloadRequested = false
        }
        // Only explicit load requests schedule another pass; overlapping requests coalesce.
        repeat {
            reloadRequested = false
            await loadSnapshot()
        } while reloadRequested && !Task.isCancelled
    }

    private func loadSnapshot() async {
        state = .loading
        do {
            async let statsResponse: EngineeringQueueStatsSummaryResponse = client.send(.mqStats)
            async let queuesResponse: EngineeringQueueStatsResponse = client.send(.mqQueues)
            async let scheduledResponse: EngineeringScheduledJobsResponse = client.send(.mqScheduledJobs)
            async let backfillsResponse: EngineeringBackfillsResponse = client.send(.mqBackfills)

            let loadedStats = try await statsResponse
            let loadedQueues = try await queuesResponse
            let loadedScheduled = try await scheduledResponse
            let loadedBackfills = try await backfillsResponse

            let loadedDomains: [EngineeringQueueDomain]
            if loadedQueues.queues.contains(where: { $0.name == "background" }) {
                let response: EngineeringQueueDomainsResponse = try await client
                    .send(.mqQueueDomains(name: "background"))
                loadedDomains = response.domains
            } else {
                loadedDomains = []
            }
            domains = loadedDomains
            stats = loadedStats.stats
            queues = loadedQueues.queues
            scheduledJobs = loadedScheduled.jobs
            backfills = loadedBackfills.backfills
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

    func pauseQueue(name: String) async {
        guard name != "background" else { return }
        await performAction(key: "pause|\(name)") {
            let _: EngineeringSuccessResponse = try await self.client.send(.mqPauseQueue(name: name))
        }
    }

    func resumeQueue(name: String) async {
        await performAction(key: "resume|\(name)") {
            let _: EngineeringSuccessResponse = try await self.client.send(.mqResumeQueue(name: name))
        }
    }

    func runScheduledJob(id: String) async {
        await performAction(key: "scheduled|\(id)") {
            let _: EngineeringSuccessResponse = try await self.client.send(.mqRunScheduledJob(id: id))
        }
    }

    func runBackfill(id: String) async {
        await performAction(key: "backfill|\(id)") {
            let _: EngineeringSuccessResponse = try await self.client.send(.mqRunBackfill(id: id))
        }
    }
}
