import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

public enum FollowerDistributionTarget: Sendable, Equatable {
    case post(String)
    case rssFeedItem(String)

    func shareEndpoint() -> Endpoint {
        switch self {
        case let .post(id): .sharePostWithFollowers(postId: id)
        case let .rssFeedItem(id): .shareRssFeedItemWithFollowers(rssFeedItemId: id)
        }
    }

    func sendEndpoint(request: FollowerDistributionRequest) -> Endpoint {
        switch self {
        case let .post(id): .sendPostToFollowers(postId: id, request: request)
        case let .rssFeedItem(id): .sendRssFeedItemToFollowers(rssFeedItemId: id, request: request)
        }
    }
}

public func canDistributeToFollowers(_ post: Post, currentUserId: String?, isSignedIn: Bool) -> Bool {
    isSignedIn && currentUserId != nil && post.createdById != currentUserId && post.postType != .comment &&
        post.parentId == nil && post.privacy == .public && (post.broadcast == .everyone || post.broadcast == .users)
}

@Observable
@MainActor
public final class FollowerDistributionViewModel {
    public private(set) var recipients: [PublicUser] = []
    public private(set) var selectedRecipientIds = Set<String>()
    public private(set) var selectedRecipients: [PublicUser] = []
    public private(set) var query = ""
    public private(set) var isSearching = false
    public internal(set) var isSending = false
    public internal(set) var isSharing = false
    public private(set) var canLoadMoreRecipients = false
    public internal(set) var error: VouchaError?
    public var sendsToAllFollowers = true

    let client: APIClient
    private var currentUserId: String?
    var target: FollowerDistributionTarget
    private var nextRecipientCursor: String?
    private var searchGeneration = 0
    var contextGeneration = 0
    private var searchTask: Task<Void, Never>?

    public init(client: APIClient, currentUserId: String?, target: FollowerDistributionTarget) {
        self.client = client
        self.currentUserId = currentUserId
        self.target = target
    }

    public var canSend: Bool {
        sendsToAllFollowers || !selectedRecipientIds.isEmpty
    }

    public func replaceContext(currentUserId: String?, target: FollowerDistributionTarget) {
        guard self.currentUserId != currentUserId || self.target != target else { return }
        contextGeneration += 1
        self.currentUserId = currentUserId
        self.target = target
        isSending = false
        isSharing = false
        resetSendSheet()
    }

    public func resetSendSheet() {
        searchTask?.cancel()
        searchTask = nil
        searchGeneration += 1
        query = ""
        recipients = []
        selectedRecipientIds = []
        selectedRecipients = []
        sendsToAllFollowers = true
        isSearching = false
        nextRecipientCursor = nil
        canLoadMoreRecipients = false
        error = nil
    }

    public func toggleRecipient(_ id: String) {
        guard !sendsToAllFollowers else { return }
        if selectedRecipientIds.contains(id) {
            selectedRecipientIds.remove(id)
            selectedRecipients.removeAll { $0.id == id }
        } else if selectedRecipientIds.count < FollowerDistributionRequest.maximumSelectedRecipients {
            selectedRecipientIds.insert(id)
            if let recipient = recipients.first(where: { $0.id == id }) {
                retainSelectedRecipient(recipient)
            }
        }
    }

    public func canToggleRecipient(_ id: String) -> Bool {
        selectedRecipientIds.contains(id) ||
            selectedRecipientIds.count < FollowerDistributionRequest.maximumSelectedRecipients
    }

    public func searchRecipients(query: String) async {
        searchGeneration += 1
        let generation = searchGeneration
        self.query = query.trimmingCharacters(in: .whitespacesAndNewlines)
        recipients = []
        nextRecipientCursor = nil
        canLoadMoreRecipients = false
        guard let currentUserId else { return }
        searchTask?.cancel()
        let query = self.query.isEmpty ? nil : self.query
        let task = Task {
            do {
                try await Task.sleep(for: .milliseconds(300))
            } catch {
                return
            }
            guard !Task.isCancelled, generation == self.searchGeneration else { return }
            await loadRecipientPage(userId: currentUserId, query: query, after: nil, generation: generation)
        }
        searchTask = task
        await task.value
    }

    public func loadMoreRecipients() async {
        guard let currentUserId, let after = nextRecipientCursor, !isSearching else { return }
        await loadRecipientPage(
            userId: currentUserId,
            query: query.isEmpty ? nil : query,
            after: after,
            generation: searchGeneration
        )
    }

    private func loadRecipientPage(userId: String, query: String?, after: String?, generation: Int) async {
        isSearching = true
        defer {
            if generation == searchGeneration {
                isSearching = false
            }
        }
        do {
            let page: Page<PublicUser> = try await client.send(
                .userFollowers(userId: userId, query: query, after: after, limit: 25)
            )
            guard generation == searchGeneration, !Task.isCancelled else { return }
            recipients.append(contentsOf: page.results.filter { candidate in
                !recipients.contains(where: { $0.id == candidate.id })
            })
            for recipient in page.results where selectedRecipientIds.contains(recipient.id) {
                retainSelectedRecipient(recipient)
            }
            nextRecipientCursor = page.pageInfo.endCursor
            canLoadMoreRecipients = page.pageInfo.hasNextPage && page.pageInfo.endCursor != nil
        } catch {
            guard generation == searchGeneration, !Task.isCancelled else { return }
            self.error = vouchaError(error)
        }
    }

    private func retainSelectedRecipient(_ recipient: PublicUser) {
        if let index = selectedRecipients.firstIndex(where: { $0.id == recipient.id }) {
            selectedRecipients[index] = recipient
        } else {
            selectedRecipients.append(recipient)
        }
    }
}
