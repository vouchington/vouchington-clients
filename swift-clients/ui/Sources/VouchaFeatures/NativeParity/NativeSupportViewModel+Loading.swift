import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeSupportViewModel {
    func loadThreads(reset: Bool) async {
        guard let client else { return }
        let revision = reset ? (activeThreadListLoadRevision + 1) : activeThreadListLoadRevision
        let existing = threads
        if reset {
            activeThreadListLoadRevision = revision
            isLoadingList = true
            listErrorMessage = nil
            threadPagination.reset()
        }
        guard let request = threadPagination.beginNextPage() else { return }
        defer {
            if activeThreadListLoadRevision == revision {
                isLoadingList = false
            }
        }
        do {
            let response: SupportThreadListResponse = try await client.send(.mySupportThreads(after: request.cursor))
            guard activeThreadListLoadRevision == revision, threadPagination.isCurrent(request) else { return }
            let incoming = reset ? mergeThreads(existing: existing, refreshed: response.results) : response.results
            threadPagination.complete(
                request,
                items: incoming,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard activeThreadListLoadRevision == revision else { return }
            threadPagination.fail(request, error: error)
            listErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard activeThreadListLoadRevision == revision else { return }
            threadPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            listErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadThread(id: String) async {
        await loadThreadMessages(id: id)
    }

    func loadThreadMessages(id: String, after: String? = nil, append: Bool = false) async {
        guard let client else { return }
        detailLoadCount += 1
        isLoadingDetail = true
        detailErrorMessage = nil
        if append == false {
            selectedThreadMessages = []
            selectedThreadPageInfo = nil
        }
        defer {
            detailLoadCount -= 1
            isLoadingDetail = detailLoadCount > 0
        }
        do {
            let response: SupportThreadDetailResponse = try await client.send(
                .mySupportThread(threadId: id, after: after)
            )
            guard selectedThreadId == id else { return }
            if append {
                selectedThreadMessages.insert(contentsOf: response.messages, at: 0)
            } else {
                selectedThreadMessages = response.messages
            }
            selectedThreadPageInfo = .init(
                hasNextPage: response.pageInfo.hasNextPage,
                endCursor: response.pageInfo.endCursor
            )
            upsertThread(response.thread)
        } catch let error as VouchaError {
            guard selectedThreadId == id else { return }
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
            if append == false {
                selectedThreadMessages = []
            }
        } catch {
            guard selectedThreadId == id else { return }
            detailErrorMessage = .verbatim(error.localizedDescription)
            if append == false {
                selectedThreadMessages = []
            }
        }
    }

    private func upsertThread(_ thread: SupportThread) {
        if let index = threads.firstIndex(where: { $0.id == thread.id }) {
            threads[index] = thread
        } else {
            threads.insert(thread, at: 0)
        }
    }

    private func mergeThreads(existing: [SupportThread], refreshed: [SupportThread]) -> [SupportThread] {
        var merged = refreshed
        let refreshedIds = Set(refreshed.map(\.id))
        let preservedIds = Set([selectedThreadId].compactMap { $0 })
        for thread in existing where preservedIds.contains(thread.id) && refreshedIds.contains(thread.id) == false {
            merged.append(thread)
        }
        return merged
    }
}
