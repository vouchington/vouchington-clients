import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension LandingPagesViewModel {
    func loadSelectedPageAnalytics() async {
        analyticsLoadGeneration += 1
        let requestGeneration = analyticsLoadGeneration
        guard let client, let pageId = selectedPage?.id else {
            resetSelectedPageAnalytics()
            return
        }
        guard await refreshAnalyticsEntitlement(
            client: client,
            pageId: pageId,
            requestGeneration: requestGeneration
        ) else {
            resetSelectedPageAnalytics()
            return
        }

        let requestedPageId = pageId
        resetSelectedPageAnalytics()
        selectedPageAnalyticsState = .loading
        do {
            let response: LandingPageAnalyticsResponse = try await client.send(.myLandingPageAnalytics(pageId: pageId))
            guard selectedPage?.id == requestedPageId, analyticsLoadGeneration == requestGeneration else { return }
            selectedPageAnalytics = response.analytics
            selectedPageAnalyticsState = .loaded
        } catch let error as VouchaError {
            guard selectedPage?.id == requestedPageId, analyticsLoadGeneration == requestGeneration else { return }
            selectedPageAnalyticsState = .error(error)
        } catch {
            guard selectedPage?.id == requestedPageId, analyticsLoadGeneration == requestGeneration else { return }
            selectedPageAnalyticsState = .error(.unexpected(error.localizedDescription))
        }
    }

    func reloadSelectedPageAnalytics() async {
        await loadSelectedPageAnalytics()
    }

    func clearSelectedPageAnalytics() {
        analyticsLoadGeneration += 1
        resetSelectedPageAnalytics()
    }

    private func resetSelectedPageAnalytics() {
        selectedPageAnalytics = nil
        selectedPageAnalyticsState = .idle
    }

    private func refreshAnalyticsEntitlement(
        client: APIClient,
        pageId: String,
        requestGeneration: Int
    ) async -> Bool {
        guard requiresAuthoritativeAnalyticsMembership else { return canViewAnalytics }
        do {
            let response: MembershipResponse = try await client.send(.membershipMe)
            guard selectedPage?.id == pageId, analyticsLoadGeneration == requestGeneration else { return false }
            canViewAnalytics = response.membership.map(hasActivePlusOrProMembership) ?? false
            return canViewAnalytics
        } catch {
            guard selectedPage?.id == pageId, analyticsLoadGeneration == requestGeneration else { return false }
            canViewAnalytics = false
            return false
        }
    }

    private func hasActivePlusOrProMembership(_ membership: Membership) -> Bool {
        guard membership.status == .active || membership.status == .pastDue,
              membership.expiresAt.map({ $0 > Date() }) ?? true else { return false }
        return ["plus", "pro"].contains(membership.plan.lowercased())
    }
}
