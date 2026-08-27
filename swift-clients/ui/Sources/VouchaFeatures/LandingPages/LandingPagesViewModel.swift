import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization

@Observable
@MainActor
public final class LandingPagesViewModel {
    public var pages: [LandingPage] = []
    public internal(set) var candidates: LandingPageCandidates = .empty
    public internal(set) var selectedPage: LandingPageWithItems?
    public internal(set) var selectedPageAnalytics: LandingPageAnalytics?
    public internal(set) var selectedPageAnalyticsState: LoadState = .idle
    public internal(set) var draftItems: [LandingPageItem] = [] {
        didSet { reconcilePickerSelections() }
    }

    public internal(set) var state: LoadState = .idle
    public internal(set) var errorMessage: UiVerbatimText?

    public var title = ""
    public var subtitle = ""
    public var slug = ""
    public var newTitle = ""
    public var newSubtitle = ""
    public var newSlug = ""
    public var linkLabel = ""
    public var linkUrl = ""
    public var addType: LandingPageAddType = .link {
        didSet {
            guard oldValue != addType else { return }
            selectedCandidateID = nil
            selectedTopicID = nil
        }
    }

    public var selectedCandidateID: String?
    public var selectedTopicID: String? {
        didSet {
            guard oldValue != selectedTopicID else { return }
            selectedGroupReviewIDs = []
            selectedGroupReferralLinkIDs = []
        }
    }

    public var selectedGroupReviewIDs: Set<String> = []
    public var selectedGroupReferralLinkIDs: Set<String> = []

    let client: APIClient?
    var canViewAnalytics: Bool
    let requiresAuthoritativeAnalyticsMembership: Bool
    var initialSlug: String?
    @ObservationIgnored var analyticsLoadGeneration = 0
    var persistedMetadataBaseline = LandingPageMetadataBaseline.empty
    var persistedItemInputBaseline: [LandingPageItemInput] = []

    public init(
        client: APIClient?,
        initialSlug: String? = nil,
        canViewAnalytics: Bool = true,
        requiresAuthoritativeAnalyticsMembership: Bool = false
    ) {
        self.client = client
        self.initialSlug = initialSlug
        self.canViewAnalytics = canViewAnalytics
        self.requiresAuthoritativeAnalyticsMembership = requiresAuthoritativeAnalyticsMembership
    }

    public var isAnalyticsPaidAccessRequired: Bool {
        selectedPage != nil && !canViewAnalytics
    }

    public var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    public var canCreatePage: Bool {
        candidates.canCreateLandingPages && !isLoading
    }

    var preservesIndexSelectionAfterDelete: Bool {
        initialSlug == nil
    }

    public func load() async {
        guard !isLoading, let client = requireClient() else { return }
        state = .loading
        errorMessage = nil
        let selectedPageID = selectedPage?.id
        do {
            async let pagesResponse: LandingPagesResponse = client.send(.myLandingPages)
            async let candidatesResponse: LandingPageCandidatesResponse = client.send(.myLandingPageCandidates)
            let stagedPages = try await pagesResponse.results
            let stagedCandidates = try await candidatesResponse.candidates
            let pageID = selectedPageIDForLoad(in: stagedPages, selectedPageID: selectedPageID)
            let stagedPage: LandingPageWithItems?
            if let pageID {
                let response: LandingPageWithItemsResponse = try await client.send(.myLandingPage(id: pageID))
                stagedPage = response.landingPage
            } else {
                stagedPage = nil
            }
            commitLoad(pages: stagedPages, candidates: stagedCandidates, page: stagedPage)
            state = .loaded
        } catch {
            handle(error)
        }
    }

    public func reload() async {
        await load()
        guard case .loaded = state else { return }
        await reloadSelectedPageAnalytics()
    }

    public func selectPage(id: String) async {
        guard !isLoading, requireClient() != nil else { return }
        let isReselectingCurrentPage = selectedPage?.id == id
        state = .loading
        errorMessage = nil
        do {
            try await loadPage(id: id, preservingSamePageDrafts: isReselectingCurrentPage)
            state = .loaded
            if isReselectingCurrentPage, selectedPage?.id == id {
                await reloadSelectedPageAnalytics()
            }
        } catch {
            handle(error)
        }
    }

    public func setActivePage(_ page: LandingPageWithItems?) {
        acceptServerPage(page)
    }

    private func selectedPageIDForLoad(in pages: [LandingPage], selectedPageID: String?) -> String? {
        if let initialSlug {
            return pages.first { $0.slug == initialSlug }?.id
        }
        if let selectedPageID {
            return pages.contains { $0.id == selectedPageID } ? selectedPageID : nil
        }
        return pages.first?.id
    }

    func loadPage(id: String, preservingSamePageDrafts: Bool = false) async throws {
        guard let client else { throw LandingPagesViewModelError.missingClient }
        let detail: LandingPageWithItemsResponse = try await client.send(.myLandingPage(id: id))
        if preservingSamePageDrafts, selectedPage?.id == detail.landingPage.id {
            commitLoad(pages: pages, candidates: candidates, page: detail.landingPage)
        } else {
            setActivePage(detail.landingPage)
        }
    }

    func requireClient() -> APIClient? {
        guard let client else {
            handle(LandingPagesViewModelError.missingClient)
            return nil
        }
        return client
    }

    func handle(_ error: Error) {
        if let error = error as? VouchaError {
            state = .error(error)
            errorMessage = .verbatim(String(describing: error))
        } else {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            errorMessage = .verbatim(error.localizedDescription)
        }
    }
}

private enum LandingPagesViewModelError: LocalizedError {
    case missingClient

    var errorDescription: String? {
        "Landing pages are unavailable because the API client is not configured."
    }
}
