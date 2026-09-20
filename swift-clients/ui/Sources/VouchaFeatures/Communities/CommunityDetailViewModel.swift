import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class CommunityDetailViewModel {
    var selectedTab: CommunitySurfaceTab {
        didSet {
            guard selectedTab != oldValue else { return }
            communityLoadRevision += 1
            rowPagination.invalidateRequestsPreservingPage()
            automodPagination.invalidateRequestsPreservingPage()
            pendingReportPagination.invalidateRequestsPreservingPage()
            modmailPageRequestRevision += 1
            moderationTransparencyContinuationToken += 1
            moderationTransparencyIsLoadingOlder = false
            moderationResults = []
            modmailThreadPagination.reset(items: modmailThreadPagination.items)
            isLoadingMoreModmail = false
            modmailPaginationError = nil
        }
    }

    var applicationMessage = ""
    var applicationAnswers: [String: DecodedJSONValue] = [:]
    var inviteRecipient = ""
    var moderationTransparencyRange: ModerationTransparencyRange = .days30
    var moderationTransparencyBuckets: [ModerationTransparencyBucket] = []
    var moderationTransparencyNextCursor: String?
    var moderationTransparencyIsLoadingOlder = false
    var moderationTransparencyLoadMoreError: UiMessage?
    var moderationAnalyticsRows: [NativeRouteDestinationRow] = []
    var moderationResults: [NativeRouteDestinationRow] = []
    var moderationQueryPostId = "" {
        didSet {
            guard oldValue != moderationQueryPostId else { return }
            discardModerationResults()
        }
    }

    var statusMessage: UiMessage?
    var summary = CommunityWorkspaceSummary()
    var rowPagination = CursorPaginationState<CommunityForwardRow>()
    var postEmbedsByPostId: [String: UrlEmbed] = [:]
    var rssFeedItemEmbedsById: [String: UrlEmbed] = [:]
    var automodPagination = CursorPaginationState<CommunityForwardRow>()
    var pendingReportPagination = CursorPaginationState<CommunityPendingReport>()
    var modmailThreadPagination = CursorPaginationState<CommunityModmailThread>()
    var modmailEndCursor: String?
    var isLoadingMoreModmail = false
    var modmailPaginationError: UiMessage?
    @ObservationIgnored
    var modmailRowIds: Set<String> = []
    @ObservationIgnored
    var modmailLoadRevision = 0
    @ObservationIgnored
    var communityLoadRevision = 0
    @ObservationIgnored
    var moderationResultsRequestRevision = 0
    @ObservationIgnored
    var moderationTransparencyContinuationToken = 0
    @ObservationIgnored
    var modmailPageRequestRevision = 0
    var applicationQuestions: [CommunityApplicationQuestion] = []
    var communityDetail: CommunityResponse?
    var listItemCounts: CommunityListItemCounts?
    var suppressCommunityDigestsWhileOnVacation = false
    var state: CommunitySurfaceState = .idle

    let client: APIClient?
    let isAdministrator: Bool
    let isSiteModerator: Bool
    let isApplicationFormRoute: Bool
    let slug: String
    let modmailThreadId: String?

    var hasDurableSiteModerationAccess: Bool {
        isSiteModerator
    }

    init(
        client: APIClient?,
        slug: String,
        initialTab: CommunitySurfaceTab = .posts,
        isAdministrator: Bool = false,
        isSiteModerator: Bool = false,
        isApplicationFormRoute: Bool = false,
        modmailThreadId: String? = nil
    ) {
        self.client = client
        self.slug = slug
        self.isAdministrator = isAdministrator
        self.isSiteModerator = isSiteModerator
        self.isApplicationFormRoute = isApplicationFormRoute
        self.modmailThreadId = modmailThreadId
        selectedTab = initialTab
    }

    func beginModmailPagination() {
        isLoadingMoreModmail = true
        modmailPaginationError = nil
    }

    func finishModmailPagination() {
        isLoadingMoreModmail = false
    }

    func failModmailPagination() {
        modmailPaginationError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)
    }

    func succeedModmailPagination() {
        modmailPaginationError = nil
    }
}
