import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

struct FriendRecommendationTombstone {
    enum Status: Equatable {
        case pending
        case succeeded
    }

    let token: UUID
    let recommendation: FriendRecommendation
    let originalIndex: Int
    var status: Status
}

@Observable
@MainActor
public final class FriendRecommendationsViewModel {
    var pagination = CursorPaginationState<FriendRecommendation>()
    public internal(set) var users: [String: PublicUser] = [:]
    public internal(set) var identity: PrivateUser?
    public internal(set) var mutationErrorMessage: UiVerbatimText?
    var mutationTokens: [String: UUID] = [:]
    var tombstones: [String: FriendRecommendationTombstone] = [:]

    let client: APIClient
    private let config: AppConfig

    public init(client: APIClient, config: AppConfig = .shared) {
        self.client = client
        self.config = config
    }

    public var recommendations: [FriendRecommendation] {
        pagination.items
    }

    public var state: LoadState {
        pagination.state
    }

    public var hasMore: Bool {
        pagination.hasMore && (pagination.endCursor != nil || pagination.lastError != nil)
    }

    public var isLoadingMore: Bool {
        pagination.isLoading && pagination.hasLoadedPage
    }

    public var hasPaginationError: Bool {
        pagination.lastError != nil
    }

    public var hasConnectedProvider: Bool {
        identity?.facebookAccount != nil || identity?.xAccount != nil || identity?.githubAccount != nil
    }

    public func user(for recommendation: FriendRecommendation) -> PublicUser? {
        users[recommendation.id]
    }

    public func avatarURL(for recommendation: FriendRecommendation) -> String? {
        config.imageURL(forImageId: user(for: recommendation)?.profileImageId)
    }

    public func isMutating(userId: String) -> Bool {
        mutationTokens[userId] != nil
    }
}
