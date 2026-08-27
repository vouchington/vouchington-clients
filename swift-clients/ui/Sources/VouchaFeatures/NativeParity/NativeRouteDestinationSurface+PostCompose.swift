import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    @ViewBuilder
    var postComposeContent: some View {
        if isAdminOnlyPostComposeRoute, !isSignedIn {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftEmptyStateSignInAdminPostsMessage)
            )
        } else if isAdminOnlyPostComposeRoute, !isAdministrator {
            EmptyStateView(
                icon: "lock.shield",
                title: .message(.nativeSwiftEmptyStateAdministratorRequired),
                message: .message(.nativeSwiftEmptyStateAdministratorPostsOnlyMessage)
            )
        } else {
            NativePostComposeSurface(
                client: viewModel.client,
                communityIdOrSlug: composeCommunityIdOrSlug,
                initialPostType: NativeRouteSurfaceViewModel.composePostType(for: viewModel.routeMatch?.path),
                isAdministrator: isAdministrator,
                turnstileSiteKey: resolvedTurnstileSiteKey
            )
        }
    }
}
