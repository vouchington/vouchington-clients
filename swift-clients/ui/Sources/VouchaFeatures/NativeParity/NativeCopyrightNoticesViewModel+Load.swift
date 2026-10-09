import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeCopyrightNoticesViewModel {
    func performInitialLoad(generation: Int) async {
        guard let client else {
            errorMessage = .message(.nativeCommonSomethingWentWrong)
            clearInitialLoadTask(generation: generation)
            return
        }
        isLoading = true
        errorMessage = nil
        defer {
            if generation == initialLoadGeneration {
                isLoading = false
                if errorMessage == nil {
                    hasLoadedInitial = true
                }
                initialLoadTask = nil
            }
        }
        do {
            euSettlements = []
            euSettlementsPageInfo = nil
            if let noticeId {
                try await loadDetail(id: noticeId, client: client, generation: generation)
            } else {
                let response: CopyrightNoticesResponse = try await client.send(.copyrightNotices())
                try requireCurrentLoad(generation: generation)
                notices = response.copyrightNotices
                pageInfo = response.pageInfo
            }
        } catch {
            guard generation == initialLoadGeneration else { return }
            errorMessage = .message(.nativeCommonSomethingWentWrong)
        }
    }

    private func loadDetail(id: String, client: APIClient, generation: Int) async throws {
        var loadedParticipant: CopyrightNotice?
        do {
            let response: CopyrightNoticeResponse = try await client.send(.copyrightParticipantNotice(id: id))
            try requireCurrentLoad(generation: generation)
            loadedParticipant = response.copyrightNotice
            participantNotice = response.copyrightNotice
            if response.copyrightNotice.jurisdiction == .euDSA {
                notice = response.copyrightNotice
                euSettlements = response.copyrightNotice.eu?.disputeSettlements ?? []
                euSettlementsPageInfo = response.copyrightNotice.eu?.disputeSettlementsPageInfo
                return
            }
        } catch let error as VouchaError {
            try requireCurrentLoad(generation: generation)
            switch error {
            case .forbidden, .notFound:
                participantNotice = nil
            default:
                throw error
            }
        }
        let detailResponse: CopyrightNoticeResponse = try await client.send(.copyrightNotice(id: id))
        try requireCurrentLoad(generation: generation)
        notice = detailResponse.copyrightNotice
        if let loadedParticipant {
            notice = withParticipantTimeline(detailResponse.copyrightNotice, participantNotice: loadedParticipant)
        }
    }

    private func requireCurrentLoad(generation: Int) throws {
        guard generation == initialLoadGeneration, !Task.isCancelled else {
            throw CancellationError()
        }
    }

    private func clearInitialLoadTask(generation: Int) {
        guard generation == initialLoadGeneration else { return }
        isLoading = false
        initialLoadTask = nil
    }

    private func withParticipantTimeline(
        _ detail: CopyrightNotice,
        participantNotice: CopyrightNotice
    ) -> CopyrightNotice {
        var detail = detail
        detail.timeline = participantNotice.timeline
        return detail
    }
}
