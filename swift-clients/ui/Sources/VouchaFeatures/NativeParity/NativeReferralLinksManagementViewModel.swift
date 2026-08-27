import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

public struct NativeReferralProgramChoice: Identifiable, Hashable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
}

@Observable
@MainActor
public final class NativeReferralLinksManagementViewModel {
    var linkPagination = CursorPaginationState<NativeReferralLink>()
    var clickPagination = CursorPaginationState<ReferralClickLogEntry>()
    public private(set) var links: [NativeReferralLink] {
        get { linkPagination.items }
        set { linkPagination.replaceItems(newValue) }
    }

    public private(set) var clicks: [ReferralClickLogEntry] {
        get { clickPagination.items }
        set { clickPagination.replaceItems(newValue) }
    }

    public internal(set) var clickUsers: [String: ReferralClickLogUser] = [:]
    public private(set) var programs: [NativeReferralProgramChoice] = []
    public private(set) var hasMoreLinks: Bool {
        get { linkPagination.hasLoadedPage && linkPagination.hasMore }
        set { linkPagination.restoreContinuation(endCursor: linkPagination.endCursor, hasMore: newValue) }
    }

    public private(set) var hasMoreClicks: Bool {
        get { clickPagination.hasLoadedPage && clickPagination.hasMore }
        set { clickPagination.restoreContinuation(endCursor: clickPagination.endCursor, hasMore: newValue) }
    }

    public internal(set) var state: LoadState = .idle
    var hasLoadedInitialContent = false

    let client: APIClient
    private var isLoadingFullContent = false

    public init(client: APIClient) {
        self.client = client
    }

    public func load() async {
        guard !isLoadingFullContent else { return }
        let snapshot = NativeReferralLinksManagementSnapshot(viewModel: self)
        isLoadingFullContent = true
        defer { isLoadingFullContent = false }
        if await load({
            try await loadLinksPage(after: nil, append: false)
            try await loadClicksPage(after: nil)
        }) {
            hasLoadedInitialContent = true
        } else {
            snapshot.restore(to: self)
        }
    }

    public func loadLinks() async {
        _ = await load {
            try await loadLinksPage(after: nil, append: false)
        }
    }

    public func loadMoreLinks() async {
        guard hasMoreLinks, !isLoadingFullContent else { return }
        _ = await load {
            try await loadLinksPage(after: linkPagination.endCursor, append: true)
        }
    }

    public func loadClicks(after: String? = nil) async {
        _ = await load {
            try await loadClicksPage(after: after)
        }
    }

    public func loadMoreClicks() async {
        guard hasMoreClicks, !isLoadingFullContent else { return }
        await loadClicks(after: clickPagination.endCursor)
    }

    public func searchReferralPrograms(query: String) async {
        _ = await load {
            let response: TopicSearchResponse = try await client.send(
                .topics(query: query, topicTypes: ["referral_program"], limit: 10)
            )
            programs = response.results.compactMap { result in
                guard let topic = response.topics[result.id] else { return nil }
                return NativeReferralProgramChoice(id: topic.id, name: topic.name, slug: topic.slug)
            }
        }
    }

    public func fetchValidationInfo(referralProgramId: String) async -> ReferralProgramValidationInfo? {
        do {
            let response: ReferralProgramValidationInfoResponse = try await client.send(
                .referralProgramValidationInfo(topicId: referralProgramId)
            )
            return response.validationInfo
        } catch VouchaError.notFound {
            return nil
        } catch let error as VouchaError {
            state = .error(error)
            return nil
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            return nil
        }
    }

    public func create(referralProgramId: String, url: String, label: String?) async -> Bool {
        await mutate(.createReferralLink(referralProgramId: referralProgramId, url: url, label: label))
    }

    public func rename(id: String, label: String?) async {
        _ = await mutate(.updateReferralLink(id: id, label: label))
    }

    public func setActive(id: String, active: Bool) async {
        _ = await mutate(active ? .activateReferralLink(id: id) : .deactivateReferralLink(id: id))
    }

    public func delete(id: String) async {
        _ = await mutate(.deleteReferralLink(id: id))
    }
}
