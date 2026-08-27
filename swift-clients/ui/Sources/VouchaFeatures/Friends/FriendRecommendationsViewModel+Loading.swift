import VouchaAPI
import VouchaCore
import VouchaModels

public extension FriendRecommendationsViewModel {
    func load() async {
        await loadPage(append: false, clearSucceededTombstones: false)
        await loadIdentity()
    }

    func loadMore() async {
        await loadPage(append: true, clearSucceededTombstones: false)
    }

    func reload() async {
        pagination.reset()
        users = [:]
        await loadPage(append: false, clearSucceededTombstones: true)
        await loadIdentity()
    }

    private func loadPage(append: Bool, clearSucceededTombstones: Bool) async {
        let request = append ? pagination.beginNextPage() : pagination.beginInitialPageIfNeeded()
        guard let request else { return }
        do {
            let response: FriendRecommendationsResponse = try await client.send(
                .friendRecommendations(after: request.cursor)
            )
            guard pagination.isCurrent(request) else { return }
            let hiddenIds = Set(tombstones.keys)
            users.merge(response.users) { _, incoming in incoming }
            let visible = response.results.filter { !hiddenIds.contains($0.id) }
            guard pagination.complete(
                request,
                items: visible,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            if clearSucceededTombstones {
                tombstones = tombstones.filter { $0.value.status == .pending }
            }
        } catch is CancellationError {
            pagination.cancel(request)
        } catch let error as VouchaError {
            pagination.fail(request, error: error)
        } catch {
            pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

    private func loadIdentity() async {
        struct IdentityEnvelope: Decodable {
            let identity: PrivateUser
        }
        do {
            let response: IdentityEnvelope = try await client.send(.myIdentity)
            identity = response.identity
        } catch {
            identity = nil
        }
    }
}
