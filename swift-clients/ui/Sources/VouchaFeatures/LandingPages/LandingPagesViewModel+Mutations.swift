import Foundation
import VouchaAPI

public extension LandingPagesViewModel {
    func createPage(title: String? = nil, slug: String? = nil, subtitle: String? = nil) async {
        guard !isLoading, let client = requireClient() else { return }
        let metadata = trimmedMetadata(
            title: title ?? newTitle,
            subtitle: subtitle ?? newSubtitle,
            slug: slug ?? newSlug
        )
        guard !metadata.title.isEmpty, !metadata.slug.isEmpty else { return }
        state = .loading
        errorMessage = nil
        do {
            let response: LandingPageResponse = try await client.send(.createMyLandingPage(
                title: metadata.title,
                subtitle: metadata.subtitlePatch,
                slug: metadata.slug
            ))
            try await loadPage(id: response.landingPage.id)
            pages = sortedReplacing(page: selectedPage?.summary ?? response.landingPage, in: pages)
            if initialSlug != nil {
                initialSlug = response.landingPage.slug
            }
            newTitle = ""
            newSubtitle = ""
            newSlug = ""
            state = .loaded
        } catch {
            handle(error)
        }
    }

    func saveDetails() async {
        guard !isLoading, let client = requireClient(), let selectedPage else { return }
        let metadata = trimmedMetadata(title: title, subtitle: subtitle, slug: slug)
        state = .loading
        errorMessage = nil
        do {
            let response: LandingPageResponse = try await client.send(.updateMyLandingPage(
                id: selectedPage.id,
                body: LandingPageMetadataBody(
                    title: metadata.title,
                    subtitle: metadata.subtitlePatch,
                    slug: metadata.slug
                )
            ))
            pages = sortedReplacing(page: response.landingPage, in: pages)
            replaceSelectedSummary(response.landingPage, items: draftItems)
            persistedMetadataBaseline = LandingPageMetadataBaseline(page: response.landingPage)
            title = response.landingPage.title
            subtitle = response.landingPage.subtitle ?? ""
            slug = response.landingPage.slug
            if initialSlug != nil {
                initialSlug = response.landingPage.slug
            }
            state = .loaded
        } catch {
            handle(error)
        }
    }

    func setDefault() async {
        guard !isLoading, let client = requireClient(), let selectedPage else { return }
        state = .loading
        errorMessage = nil
        do {
            let response: LandingPageResponse = try await client.send(
                .setDefaultMyLandingPage(id: selectedPage.id)
            )
            pages = pages
                .map { page in
                    page.id == response.landingPage.id
                        ? response.landingPage
                        : pageWithDefault(page, isDefault: false)
                }
                .sorted { lhs, rhs in
                    if lhs.isDefault != rhs.isDefault {
                        return lhs.isDefault && !rhs.isDefault
                    }
                    return lhs.createdAt < rhs.createdAt
                }
            replaceSelectedSummary(response.landingPage, items: draftItems, preservingDraftMetadata: true)
            state = .loaded
        } catch {
            handle(error)
        }
    }

    func deleteSelectedPage() async {
        guard !isLoading, let client = requireClient(), let selectedPage else { return }
        state = .loading
        errorMessage = nil
        do {
            let _: LandingPageEmptyResponse = try await client.send(.deleteMyLandingPage(id: selectedPage.id))
            setActivePage(nil)
            let response: LandingPagesResponse = try await client.send(.myLandingPages)
            pages = response.results
            if preservesIndexSelectionAfterDelete, let first = pages.first {
                try await loadPage(id: first.id)
            } else {
                setActivePage(nil)
            }
            state = .loaded
        } catch {
            handle(error)
        }
    }

    func saveContent() async {
        guard !isLoading, let client = requireClient(), let selectedPage else { return }
        state = .loading
        errorMessage = nil
        do {
            let response: LandingPageWithItemsResponse = try await client.send(.replaceMyLandingPageItems(
                id: selectedPage.id,
                items: draftItems.map(\.input)
            ))
            draftItems = response.landingPage.items
            persistedItemInputBaseline = response.landingPage.items.map(\.input)
            replaceSelectedSummary(response.landingPage.summary, items: draftItems, preservingDraftMetadata: true)
            clearSelectedPageAnalytics()
            pages = sortedReplacing(page: response.landingPage.summary, in: pages)
            state = .loaded
        } catch {
            handle(error)
        }
    }

}

private struct LandingPageEmptyResponse: Decodable {}
