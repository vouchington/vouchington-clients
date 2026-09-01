import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

public enum NativeCommentThreadSort: String, CaseIterable, Sendable {
    case new, best
}

public struct NativeCommentThreadNode: Identifiable, Sendable {
    public let post: Post
    public let children: [NativeCommentThreadNode]

    public var id: String {
        post.id
    }

    public init(post: Post, children: [NativeCommentThreadNode] = []) {
        self.post = post
        self.children = children
    }
}

@Observable
@MainActor
public final class NativeCommentThreadViewModel {
    public let rootPostId: String
    public let currentUserId: String?
    public var sort: NativeCommentThreadSort = .new {
        didSet {
            if oldValue != sort {
                rebuildCommentTree()
            }
        }
    }

    public internal(set) var threadState: LoadState = .idle
    public internal(set) var ancestorState: LoadState = .idle
    public internal(set) var focusedState: LoadState = .idle
    public internal(set) var rootPost: Post?
    public internal(set) var ancestorPosts: [Post] = []
    public var focusedCommentId: String?
    public internal(set) var commentTree: [NativeCommentThreadNode] = []
    public internal(set) var collapsedCommentIds: Set<String> = []
    public internal(set) var postElectionsById: [String: PostElection] = [:]
    public internal(set) var electionVotesById: [String: PostVote] = [:]
    public internal(set) var postEmbedsByPostId: [String: UrlEmbed] = [:]
    public var bookmarksByPostId: [String: [String: Bool]] = [:]
    public var voteChoicesByPostId: [String: ElectionVoteChoice] = [:]
    public internal(set) var inFlightVotePostIds: Set<String> = []
    public var mutationState: LoadState = .idle
    let emailVerificationGate = EmailVerificationGatedMutation()

    let client: APIClient?
    var descendantPosts: [Post] = []
    var descendantPagination = CursorPaginationState<Post>()

    public init(client: APIClient?, rootPostId: String, currentUserId: String? = nil) {
        self.client = client
        self.rootPostId = rootPostId
        self.currentUserId = currentUserId
    }

    public var collapseStorageKey: String {
        Self.collapseStorageKey(rootPostId: rootPostId, userId: currentUserId)
    }

    public var isLoading: Bool {
        if case .loading = threadState {
            return true
        }
        if case .loading = ancestorState {
            return true
        }
        if case .loading = focusedState {
            return true
        }
        return false
    }

    public func setSort(_ sort: NativeCommentThreadSort) {
        self.sort = sort
    }

    public func toggleCollapse(commentId: String) {
        if collapsedCommentIds.contains(commentId) {
            collapsedCommentIds.remove(commentId)
        } else {
            collapsedCommentIds.insert(commentId)
        }
    }

    public static func collapseStorageKey(rootPostId: String, userId: String? = nil) -> String {
        "comments-collapsed:\(rootPostId)\(userId.map { ":\($0)" } ?? "")"
    }

}

extension NativeCommentThreadViewModel {
    func normalizedPermalinkTarget(_ targetCommentId: String) -> String? {
        let trimmed = targetCommentId.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func loadThreadForBlankPermalinkTarget() async {
        focusedCommentId = nil
        ancestorState = .idle
        focusedState = .idle
        await loadThread()
    }

    func setMissingClientError(includePermalinkStates: Bool) {
        let error = VouchaError.api(statusCode: 0, preconditionCode: nil)
        if includePermalinkStates {
            ancestorState = .error(error)
            focusedState = .error(error)
        }
        threadState = .error(error)
    }

}
