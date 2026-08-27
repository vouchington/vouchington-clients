import VouchaAPI

struct LandingPageMetadataBaseline: Equatable {
    static let empty = LandingPageMetadataBaseline(title: "", subtitle: "", slug: "")

    let title: String
    let subtitle: String
    let slug: String

    init(title: String, subtitle: String, slug: String) {
        self.title = title
        self.subtitle = subtitle
        self.slug = slug
    }

    init(page: LandingPageWithItems) {
        self.init(title: page.title, subtitle: page.subtitle ?? "", slug: page.slug)
    }

    init(page: LandingPage) {
        self.init(title: page.title, subtitle: page.subtitle ?? "", slug: page.slug)
    }
}

extension LandingPagesViewModel {
    public var hasUnsavedMetadata: Bool {
        LandingPageMetadataBaseline(title: title, subtitle: subtitle, slug: slug) != persistedMetadataBaseline
    }

    public var hasUnsavedItems: Bool {
        draftItems.map(\.input) != persistedItemInputBaseline
    }

    func acceptServerPage(_ page: LandingPageWithItems?) {
        selectedPage = page
        clearSelectedPageAnalytics()
        persistedMetadataBaseline = page.map(LandingPageMetadataBaseline.init(page:)) ?? .empty
        persistedItemInputBaseline = page?.items.map(\.input) ?? []
        draftItems = page?.items ?? []
        title = page?.title ?? ""
        subtitle = page?.subtitle ?? ""
        slug = page?.slug ?? ""
        resetTransientPickerInput()
    }

    func commitLoad(
        pages stagedPages: [LandingPage],
        candidates stagedCandidates: LandingPageCandidates,
        page stagedPage: LandingPageWithItems?
    ) {
        let samePage = stagedPage?.id == selectedPage?.id && stagedPage != nil
        let preservedMetadata = LandingPageMetadataBaseline(title: title, subtitle: subtitle, slug: slug)
        let preservedItems = draftItems
        let preserveMetadata = samePage && hasUnsavedMetadata
        let preserveItems = samePage && hasUnsavedItems
        let priorPageID = selectedPage?.id

        pages = stagedPages
        candidates = stagedCandidates
        guard let stagedPage else {
            acceptServerPage(nil)
            return
        }

        selectedPage = stagedPage
        if priorPageID != stagedPage.id {
            clearSelectedPageAnalytics()
        }
        persistedMetadataBaseline = LandingPageMetadataBaseline(page: stagedPage)
        persistedItemInputBaseline = stagedPage.items.map(\.input)
        if preserveMetadata {
            title = preservedMetadata.title
            subtitle = preservedMetadata.subtitle
            slug = preservedMetadata.slug
        } else {
            title = stagedPage.title
            subtitle = stagedPage.subtitle ?? ""
            slug = stagedPage.slug
        }
        draftItems = preserveItems ? preservedItems : stagedPage.items
        replaceSelectedSummary(stagedPage.summary, items: draftItems, preservingDraftMetadata: true)
        if samePage {
            reconcilePickerSelections()
        } else {
            resetTransientPickerInput()
        }
    }

    func replaceSelectedSummary(
        _ summary: LandingPage,
        items: [LandingPageItem],
        preservingDraftMetadata: Bool = false
    ) {
        selectedPage = LandingPageWithItems(
            id: summary.id,
            userId: summary.userId,
            title: preservingDraftMetadata ? title : summary.title,
            subtitle: preservingDraftMetadata ? subtitle : summary.subtitle,
            slug: preservingDraftMetadata ? slug : summary.slug,
            isDefault: summary.isDefault,
            createdAt: summary.createdAt,
            updatedAt: summary.updatedAt,
            items: items
        )
    }

    private func resetTransientPickerInput() {
        addType = .link
        linkLabel = ""
        linkUrl = ""
        clearTransientPickerSelection()
    }
}
