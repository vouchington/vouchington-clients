import SwiftUI
import VouchaAPI
import VouchaDesignSystem

struct CommunitySurface: View {
    private let client: APIClient?
    private let mode: CommunitySurfaceMode
    private let isSignedIn: Bool
    private let isAdministrator: Bool
    private let isSiteModerator: Bool
    private let showSignIn: () -> Void
    private let onNavigate: (String) -> Void
    private let turnstileSiteKey: String?

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        isSignedIn: Bool = true,
        isAdministrator: Bool = false,
        isSiteModerator: Bool = false,
        turnstileSiteKey: String?,
        showSignIn: @escaping () -> Void = {},
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        self.client = client
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.isSiteModerator = isSiteModerator
        self.showSignIn = showSignIn
        self.onNavigate = onNavigate
        mode = CommunitySurfaceMode.mode(for: routeMatch)
        self.turnstileSiteKey = turnstileSiteKey
    }

    var body: some View {
        switch mode {
        case .browse:
            CommunityBrowseSurface(viewModel: CommunityBrowseViewModel(client: client))
        case .create:
            if isSignedIn {
                CommunityCreateSurface(
                    viewModel: CommunityCreateViewModel(client: client),
                    client: client,
                    turnstileSiteKey: turnstileSiteKey
                )
            } else {
                EmptyStateView(
                    icon: "person.crop.circle.badge.plus",
                    title: .message(.nativeSwiftEmptyStateSignInRequired),
                    message: .message(.nativeSwiftEmptyStateSignInCommunityMessage),
                    actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                    action: showSignIn
                )
            }
        case let .invite(code):
            CommunityInviteRedemptionSurface(
                viewModel: CommunityInviteRedemptionViewModel(client: client, code: code),
                isSignedIn: isSignedIn,
                showSignIn: showSignIn
            )
        case let .detail(slug, tab, isApplicationFormRoute, modmailThreadId):
            CommunityWorkspaceSurface(
                viewModel: CommunityDetailViewModel(
                    client: client,
                    slug: slug,
                    initialTab: tab,
                    isAdministrator: isAdministrator,
                    isSiteModerator: isSiteModerator,
                    isApplicationFormRoute: isApplicationFormRoute,
                    modmailThreadId: modmailThreadId
                ),
                isSignedIn: isSignedIn,
                showSignIn: showSignIn,
                onNavigate: onNavigate
            )
        }
    }
}
