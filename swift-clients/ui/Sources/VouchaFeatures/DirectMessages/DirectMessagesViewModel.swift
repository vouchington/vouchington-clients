import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

@Observable
@MainActor
public final class DirectMessagesViewModel {
    var conversationPagination = CursorPaginationState<DirectConversation>()
    public internal(set) var conversations: [DirectConversation] {
        get { conversationPagination.items }
        set { conversationPagination.replaceItems(newValue) }
    }

    public internal(set) var messages: [DirectMessage] = []
    public internal(set) var participants: [ConversationParticipant] = []
    public internal(set) var userResults: [PublicUser] = []
    public internal(set) var participantUserResults: [PublicUser] = []
    public internal(set) var state: LoadState = .idle
    public internal(set) var threadState: LoadState = .idle
    public internal(set) var hasMoreConversations: Bool {
        get { conversationPagination.hasMore }
        set {
            conversationPagination.restoreContinuation(
                endCursor: conversationPagination.endCursor,
                hasMore: newValue
            )
        }
    }

    public internal(set) var hasMoreMessages = true
    public internal(set) var selectedConversationId: String?
    public internal(set) var participantAddPolicy: ConversationParticipantAddPolicy = .ownerOnly

    public var isOwner: Bool {
        guard let currentUserId else { return false }
        return participants.contains { $0.userId == currentUserId && $0.role == "owner" }
    }

    public var canAddParticipants: Bool {
        isOwner || participantAddPolicy == .allMembers
    }

    let client: APIClient
    public let currentUserId: String?
    var messageCursor: String?
    private var pendingInboxReload = false
    var threadLoadGeneration = 0
    var loadingMoreMessageRequestKeys = Set<String>()
    var isCreatingConversation = false
    var nextUserSearchRequestID = 0
    var nextParticipantSearchRequestID = 0

    public init(client: APIClient, currentUserId: String? = nil) {
        self.client = client
        self.currentUserId = currentUserId
    }

    var isLoadingConversations: Bool {
        conversationPagination.isLoading
    }

    var conversationLoadError: VouchaError? {
        conversationPagination.lastError
    }

    public func loadInbox() async {
        guard conversations.isEmpty else { return }
        await reloadInbox()
    }

    public func reloadInbox() async {
        guard !conversationPagination.isLoading else {
            pendingInboxReload = true
            return
        }
        await reloadInboxReplacingExistingConversations()
    }

    public func loadMoreConversations() async {
        await loadNextConversationPage()
    }

    private func reloadInboxReplacingExistingConversations() async {
        pendingInboxReload = false
        let existingPagination = conversationPagination
        conversationPagination.reset()
        await loadNextConversationPage()
        if case .error = state {
            conversationPagination = existingPagination
        }
    }

    private func loadNextConversationPage() async {
        guard let request = conversationPagination.beginNextPage() else { return }
        defer {
            if pendingInboxReload {
                pendingInboxReload = false
                Task { await reloadInboxReplacingExistingConversations() }
            }
        }
        state = .loading
        do {
            let page: Page<DirectConversation> = try await client.send(.myMessages(after: request.cursor))
            guard !pendingInboxReload else {
                conversationPagination.cancel(request)
                return
            }
            guard conversationPagination.isCurrent(request) else { return }
            conversationPagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
            state = .loaded
        } catch {
            guard !pendingInboxReload else { return }
            let error = vouchaError(from: error)
            conversationPagination.fail(request, error: error)
            state = .error(error)
        }
    }

    func removeConversationFromInbox(conversationId: String) {
        conversations.removeAll { $0.id == conversationId }
    }

    func vouchaError(from error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}
