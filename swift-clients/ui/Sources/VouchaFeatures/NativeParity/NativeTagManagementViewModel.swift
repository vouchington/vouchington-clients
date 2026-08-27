import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class NativeTagManagementViewModel {
    let client: APIClient?
    let routeMatch: NativeRouteMatch?
    let subjectKind: NativeTagManagementSubjectKind
    let explicitSubjectId: String?
    let explicitSubjectTitle: String?

    var state: LoadState = .idle
    var subjectId: String = ""
    var subjectTitle: UiVerbatimText?
    var subjectDetail: UiVerbatimText?
    var topicType: String?
    var tabs: [NativeTagRelationTab] = []
    var relations: [EntityRelation] = []
    var relationPagination = CursorPaginationState<EntityRelation>()
    var electionVotes: [String: EntityRelationVote] = [:]
    var publisherTypes: [PublisherTypeTopic] = []
    var searchState: LoadState = .idle
    var searchResults: [NativeGenericEntity] = []
    var tagLimitReached = false

    var activeTab = "" {
        didSet {
            guard activeTab != oldValue else { return }
            relationPagination.invalidateRequestsPreservingPage()
            // Each relation tab has its own tag-limit budget, so a cap reached on one tab must
            // not keep hiding the add-tag form on another.
            tagLimitReached = false
        }
    }

    var searchQuery = ""
    var publisherTypeSelection = ""
    var inFlightMutationKeys: Set<String> = []

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        subjectKind: NativeTagManagementSubjectKind,
        subjectId: String? = nil,
        subjectTitle: String? = nil
    ) {
        self.client = client
        self.routeMatch = routeMatch
        self.subjectKind = subjectKind
        explicitSubjectId = subjectId
        explicitSubjectTitle = subjectTitle
    }

    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    var isMutating: Bool {
        !inFlightMutationKeys.isEmpty
    }

    var activeTabConfig: NativeTagRelationTab? {
        tabs.first { $0.value == activeTab }
    }
}
