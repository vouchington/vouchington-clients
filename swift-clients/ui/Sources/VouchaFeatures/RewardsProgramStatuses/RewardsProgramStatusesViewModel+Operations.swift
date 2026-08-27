import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension RewardsProgramStatusesViewModel {
    func create(rewardsProgramStatusId: String) async -> Bool {
        guard !isCreating else { return false }
        isCreating = true
        mutationErrorMessage = nil
        defer { isCreating = false }
        do {
            let status = try await service.create(body: .init(rewardsProgramStatusId: rewardsProgramStatusId))
            try Task.checkCancellation()
            upsert(status)
            return true
        } catch {
            guard !isCancellation(error) else { return false }
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func save(status: RewardsProgramStatus, draft: RewardsProgramStatusDraft) async -> Bool {
        guard !mutatingIds.contains(status.id) else { return false }
        guard draft.hasValidDateRange else {
            mutationErrorMessage = .message(.extractedMyRewardsProgramStatusesManagerSinceMustBeBeforeUntilA4745364)
            return false
        }
        guard let body = draft.updateBody(comparedWith: status) else { return true }
        mutatingIds.insert(status.id)
        mutationErrorMessage = nil
        defer { mutatingIds.remove(status.id) }
        do {
            let updated = try await service.update(id: status.id, body: body)
            try Task.checkCancellation()
            upsert(updated)
            return true
        } catch {
            guard !isCancellation(error) else { return false }
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func delete(_ status: RewardsProgramStatus) async -> Bool {
        guard !mutatingIds.contains(status.id), let rank = statuses.firstIndex(where: { $0.id == status.id }) else {
            return false
        }
        mutatingIds.insert(status.id)
        pendingDeletions[status.id] = .init(pendingReadIds: inFlightReadIds)
        let removedUpsert = localUpserts.removeValue(forKey: status.id)
        statuses = statuses.filter { $0.id != status.id }
        defer { mutatingIds.remove(status.id) }
        do {
            try await service.delete(id: status.id)
            try Task.checkCancellation()
            completeLocalDeletion(status.id)
            return true
        } catch {
            guard !isCancellation(error) else {
                restore()
                return false
            }
            restore()
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }

        func restore() {
            pendingDeletions.removeValue(forKey: status.id)
            if let removedUpsert {
                localUpserts[status.id] = removedUpsert
            }
            var restored = statuses.filter { $0.id != status.id }
            restored.insert(status, at: min(rank, restored.count))
            pagination.replaceItems(restored)
        }
    }

    func load() async {
        guard !isLoading else { return }
        pagination.reset(items: statuses)
        guard let request = pagination.beginNextPage() else { return }
        let readId = beginRead()
        isLoading = true
        errorMessage = nil
        defer {
            if pagination.isCurrent(request) {
                pagination.cancel(request)
            }
            completeRead(readId)
            isLoading = false
        }
        do {
            let page = try await service.statuses(after: nil)
            try Task.checkCancellation()
            guard pagination.isCurrent(request), pagination.complete(
                request, items: [], endCursor: page.pageInfo.endCursor, hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            pagination.replaceItems(reconciled(page.results, protecting: readId))
        } catch {
            guard !isCancellation(error) else {
                pagination.cancel(request)
                return
            }
            guard pagination.fail(request, error: paginationError(error)) else { return }
            errorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadMore() async {
        guard !isLoading, let request = pagination.beginNextPage() else { return }
        let readId = beginRead()
        defer { completeRead(readId) }
        do {
            let page = try await service.statuses(after: request.cursor)
            try Task.checkCancellation()
            guard pagination.isCurrent(request), pagination.complete(
                request, items: page.results, endCursor: page.pageInfo.endCursor, hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            pagination.replaceItems(reconciled(pagination.items, protecting: readId))
        } catch {
            if isCancellation(error) {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: paginationError(error))
            }
        }
    }

    func searchTopics() async {
        let query = topicQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else {
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
            return
        }
        guard !isSearching else { return }
        let generation = UUID()
        searchGeneration = generation
        isSearching = true
        searchErrorMessage = nil
        defer {
            if searchGeneration == generation {
                isSearching = false
            }
        }
        do {
            let results = try await service.searchStatuses(query: query)
            try Task.checkCancellation()
            guard searchGeneration == generation,
                  topicQuery.trimmingCharacters(in: .whitespacesAndNewlines) == query else { return }
            var ids = Set<String>()
            topicResults = results.filter {
                $0.topicType == "rewards_program_status" && $0.name != nil && ids.insert($0.id).inserted
            }
        } catch {
            guard searchGeneration == generation else { return }
            guard !isCancellation(error) else { return }
            searchErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    private func paginationError(_ error: Error) -> VouchaError {
        (error as? VouchaError) ?? .unexpected(error.localizedDescription)
    }

    private func isCancellation(_ error: Error) -> Bool {
        error is CancellationError || Task.isCancelled
    }
}
