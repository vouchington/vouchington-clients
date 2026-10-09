import VouchaAPI
import VouchaModels

extension NativeCopyrightNoticesViewModel {
    func loadMore() async {
        guard let client, let cursor = pageInfo?.endCursor, !isLoading else { return }
        isLoading = true
        paginationFailed = false
        errorMessage = nil
        defer { isLoading = false }
        do {
            let response: CopyrightNoticesResponse = try await client.send(.copyrightNotices(after: cursor))
            var seen = Set(notices.map(\.id))
            notices.append(contentsOf: response.copyrightNotices.filter { seen.insert($0.id).inserted })
            pageInfo = response.pageInfo
        } catch {
            paginationFailed = true
            errorMessage = nil
        }
    }

    func loadMoreSettlements() async {
        guard let client,
              let id = noticeId,
              let cursor = euSettlementsPageInfo?.endCursor,
              !isLoading else { return }
        isLoading = true
        paginationFailed = false
        errorMessage = nil
        defer { isLoading = false }
        do {
            let response: CopyrightEuDisputeSettlementsResponse = try await client.send(
                .copyrightEuDisputeSettlements(id: id, after: cursor)
            )
            var seen = Set(euSettlements.map(\.id))
            euSettlements.append(contentsOf: response.copyrightEuDisputeSettlements.filter {
                seen.insert($0.id).inserted
            })
            euSettlementsPageInfo = response.pageInfo
        } catch {
            paginationFailed = true
            errorMessage = nil
        }
    }
}
