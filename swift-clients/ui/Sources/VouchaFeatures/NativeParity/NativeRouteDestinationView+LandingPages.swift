import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    var landingPageManagementContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            NativeRouteDestinationHeader(entry: entry)
                .padding([.horizontal, .top], Spacing.md)
            LandingPagesNativeSurface(
                client: viewModel.client,
                initialSlug: ownerLandingPageSlug,
                canViewAnalytics: canViewLandingPageAnalytics
            )
        }
    }
}
