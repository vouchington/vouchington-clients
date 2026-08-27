import SwiftUI

extension NativeRouteDestinationSurface {
    var isDedicatedMemberAppealsRoute: Bool {
        MemberAppealsRoute(path: routeMatch?.path) != nil
    }

    @ViewBuilder
    var memberAppealsContent: some View {
        if let route = MemberAppealsRoute(path: routeMatch?.path) {
            MemberAppealsSurface(
                client: viewModel.client,
                isSignedIn: isSignedIn,
                currentUserId: currentUserId,
                route: route,
                turnstileSiteKey: turnstileSiteKey
            )
            .id(memberAppealsIdentity(route: route))
        }
    }

    func memberAppealsIdentity(route: MemberAppealsRoute) -> MemberAppealsSurfaceIdentity {
        .state(
            route: route,
            isSignedIn: isSignedIn,
            currentUserId: currentUserId
        )
    }
}

enum MemberAppealsSurfaceIdentity: Hashable {
    case state(route: MemberAppealsRoute, isSignedIn: Bool, currentUserId: String?)
}
