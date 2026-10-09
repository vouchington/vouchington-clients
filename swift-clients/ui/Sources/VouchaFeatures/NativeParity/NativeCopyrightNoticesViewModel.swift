import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@MainActor
@Observable
final class NativeCopyrightNoticesViewModel {
    let client: APIClient?
    let noticeId: String?
    var initialLoadTask: Task<Void, Never>?
    var initialLoadGeneration = 0
    var hasLoadedInitial = false

    var notices: [CopyrightNotice] = []
    var notice: CopyrightNotice?
    var pageInfo: CopyrightNoticesPageInfo?
    var participantNotice: CopyrightNotice?
    var euSettlements: [CopyrightEuDisputeSettlement] = []
    var euSettlementsPageInfo: CopyrightNoticesPageInfo?
    var isLoading = false
    var errorMessage: UiVerbatimText?
    var paginationFailed = false

    init(client: APIClient?, noticeId: String?) {
        self.client = client
        self.noticeId = noticeId
    }

    func loadInitial() async {
        guard !hasLoadedInitial else { return }
        if let initialLoadTask {
            await initialLoadTask.value
            return
        }
        initialLoadGeneration += 1
        let generation = initialLoadGeneration
        let task = Task { @MainActor in await performInitialLoad(generation: generation) }
        initialLoadTask = task
        await task.value
    }

    func cancelInitialLoad() {
        guard let initialLoadTask else { return }
        initialLoadGeneration += 1
        initialLoadTask.cancel()
        self.initialLoadTask = nil
        isLoading = false
        errorMessage = nil
        notice = nil
        participantNotice = nil
        notices = []
        pageInfo = nil
        euSettlements = []
        euSettlementsPageInfo = nil
    }
}
