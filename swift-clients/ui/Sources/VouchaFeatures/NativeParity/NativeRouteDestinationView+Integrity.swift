import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    func rebuildViewModels() {
        let resolvedClient = Self.resolvedClient(entry: entry, client: client, isSignedIn: isSignedIn)
        viewModel = NativeRouteSurfaceViewModel(
            entry: entry,
            client: resolvedClient,
            routeMatch: routeMatch,
            routeQuery: routeQuery,
            isAdministrator: isAdministrator
        )
        reviewQueueViewModel = NativeReviewQueueViewModel(
            client: resolvedClient,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator
        )
    }

    var scrollContent: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                if entry.destinationIdentifier != .userProfile {
                    NativeRouteDestinationHeader(entry: entry)
                }
                destinationContent
            }
            .padding(Spacing.md)
        }
    }

    @ViewBuilder
    var integrityRouteContent: some View {
        let tier = IntegrityViewerTier(
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator,
            isCustomerSupport: isCustomerSupport
        )
        switch IntegrityRouteKind(path: routeMatch?.path) {
        case .reportFlags:
            ReportIntegritySurface(
                client: viewModel.client,
                viewerTier: tier,
                onNavigate: onNavigateToTargetPath
            )
        case .voteFlags:
            VoteIntegritySurface(
                client: viewModel.client,
                viewerTier: tier,
                onNavigate: onNavigateToTargetPath
            )
        case .reportPenalties:
            IntegrityPenaltyLedgerSurface(
                client: viewModel.client,
                viewerTier: tier,
                domain: .report,
                onNavigate: onNavigateToTargetPath
            )
        case .votePenalties:
            IntegrityPenaltyLedgerSurface(
                client: viewModel.client,
                viewerTier: tier,
                domain: .vote,
                onNavigate: onNavigateToTargetPath
            )
        case nil:
            EmptyView()
        }
    }
}
