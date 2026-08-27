import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct NativeIdentityResponse: Decodable {
    let identity: PrivateUser
}

struct NativeProfileResponse: Decodable {
    let profile: Profile
}

@Observable
@MainActor
public final class NativeRouteSurfaceViewModel {
    public internal(set) var rows: [NativeRouteDestinationRow]
    public internal(set) var focusedRssFeedItem: RssFeedItem?
    public internal(set) var focusedRssFeedItemContentHtml: String?
    public internal(set) var focusedRssFeedItemElection: RssFeedItemElection?
    public internal(set) var focusedRssFeedItemBookmarks: [String: Bool] = [:]
    public internal(set) var focusedRssFeedItemVote: ElectionVoteChoice?
    var bookmarkRows: [NativeBookmarkRow] = []
    var bookmarkPagination = CursorPaginationState<NativeBookmarkRow>()
    var bookmarkMutationErrorMessage: UiVerbatimText?
    public internal(set) var actions: [NativeRouteSurfaceAction]
    public internal(set) var searchSections: [NativeSearchSection] = []
    public var fediverseProvider: FediverseProviderFilter
    public internal(set) var state: LoadState = .idle
    public internal(set) var myVotesByTopicId: [String: ElectionVoteChoice] = [:]
    public internal(set) var topicDetailId: String?
    public internal(set) var topicDetailElection: TopicElection?
    public internal(set) var hostnameDetailId: String?
    public internal(set) var hostnameDetailElection: TopicElection?
    public internal(set) var hostnameDetailVote: ElectionVoteChoice?
    var hostnameDetailHostname: NativeHostnameSummary?
    public internal(set) var detailRelationEntityType: String?
    public internal(set) var detailRelationEntityId: String?
    public internal(set) var detailRelationBookmarks: [String: Bool] = [:]
    public internal(set) var detailRelationIsSelfProfile = false
    var userProfile = NativeUserProfileState()
    public internal(set) var detailUserTags: [EntityRelation] = []
    var detailUserTagPagination = CursorPaginationState<EntityRelation>()
    public internal(set) var detailUserTagVotes: [String: EntityRelationVote] = [:]
    var detailReportTarget: NativeDetailReportTarget?
    var detailReportSubmissionState: NativeDetailReportSubmissionState = .idle
    var detailReportSuccessPresented = false
    public internal(set) var detailReportErrorMessage: UiVerbatimText?
    public internal(set) var detailPendingReportReason: String?
    public internal(set) var detailPendingReportNote = ""
    public internal(set) var detailPendingReportTurnstile = false
    public internal(set) var detailShowingReportTurnstile = false
    let emailVerificationGate = EmailVerificationGatedMutation()
    var inFlightVoteTopicIds: Set<String> = []
    var hostnameVoteInFlight = false
    var inFlightUserTagVoteIds: Set<String> = []
    var detailReportOperationGeneration = 0
    var inFlightDetailRelationKeys: Set<String> = []
    var inFlightBookmarkRowIds: Set<String> = []
    var bookmarkDestinationCache: [BookmarkDestinationIdentity: String] = [:]
    var pendingBookmarkDestinations: [BookmarkDestinationIdentity: PendingBookmarkDestination] = [:]
    var bookmarkContextId = UUID()
    var currentBookmarkCollection: NativeBookmarkCollection?
    var agentConversation: AgentConversation?
    var agentConversationMessages: [AgentConversationMessage] = []
    var agentConversationAgentSystemUserId: String?
    var agentConversationPageInfo: Page<AgentConversationMessage>.PageInfo?
    var agentConversationLoadRevision = 0
    var agentConversationPageRequestRevision = 0
    var agentConversationContinuationToken = 0
    var activeAgentConversationContinuationToken: Int?
    public var isLoadingOlderAgentConversationMessages: Bool {
        activeAgentConversationContinuationToken != nil
    }

    public internal(set) var agentConversationPaginationErrorMessage: UiVerbatimText?
    var agentDirectoryPagination = CursorPaginationState<AgentSummary>()
    var agentDirectoryUsers: [String: PublicUser] = [:]
    var agentDirectoryLoadRevision = 0
    var agentDirectoryPageRequestRevision = 0
    public internal(set) var agentDirectoryPaginationErrorMessage: UiVerbatimText?
    var agentConversationListPagination = CursorPaginationState<AgentConversationSummary>()
    var agentConversationListUsers: [String: PublicUser] = [:]
    var agentDetail: AgentDetailResponse?
    var agentConversationFilter: AgentConversationFilter?
    var agentConversationFilterRouteIdentity: String?
    var agentConversationListLoadRevision = 0
    var agentConversationListPageRequestRevision = 0
    public internal(set) var agentConversationsPageErrorMessage: UiVerbatimText?
    public internal(set) var fediverseInstancePageInfo: Page<TopicSearchResult>.PageInfo?
    public internal(set) var isLoadingMoreFediverseInstances = false
    public internal(set) var fediverseInstancePaginationErrorMessage: UiVerbatimText?
    var moderationTransparencyBuckets: [ModerationTransparencyBucket] = []
    var moderationTransparencyNextCursor: String?
    var moderationTransparencyIsLoadingOlder = false
    var moderationTransparencyLoadMoreError: UiVerbatimText?
    var moderationTransparencyLoadRevision = 0
    var moderationTransparencyPageRevision = 0
    var moderationTransparencyContinuationToken = 0
    var fediverseInstanceItems: [FediverseInstanceListItem] = []
    var agentListPageSize = 25
    var agentMessagePageSize = 50
    var searchGeneration = 0
    var forwardPagination = CursorPaginationState<NativeForwardRow>()
    var crawlHistoryPagination = CursorPaginationState<NativeForwardRow>()
    var crawlHistoryPrefix: [NativeRouteDestinationRow] = []
    var crawlHistorySuffix: [NativeRouteDestinationRow] = []
    var crawlHistoryEndpoint: NativeCrawlHistoryEndpoint?

    let client: APIClient?
    let destination: NativeRouteDestinationIdentifier?
    let routeMatch: NativeRouteMatch?
    let routeQuery: String?
    let isAdministrator: Bool

    public init(
        entry: NativeRouteCatalogEntry,
        client: APIClient?,
        routeMatch: NativeRouteMatch? = nil,
        routeQuery: String? = nil,
        isAdministrator: Bool = false
    ) {
        self.client = client
        self.routeMatch = routeMatch
        self.routeQuery = routeQuery
        self.isAdministrator = isAdministrator
        destination = entry.destinationIdentifier == .moderationReviewQueue ? nil : entry.destinationIdentifier
        fediverseProvider = FediverseProviderFilter(routeValue: routeMatch?.queryValue("provider"))
        rows = entry.destinationIdentifier?.nativeRows ?? []
        actions = entry.destinationIdentifier?.nativeActions ?? []
    }

}

enum NativeCrawlHistoryEndpoint {
    case rssFeed(id: String, detailPathPrefix: String)
    case url(String)
}
