import SwiftUI
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaDesignSystem

struct NativeRouteDestinationSurface: View {
    let entry: NativeRouteCatalogEntry
    let client: APIClient?
    let routeMatch: NativeRouteMatch?
    let routeQuery: String?
    let playbackController: PodcastPlaybackController?
    let isSignedIn: Bool
    let currentUserId: String?
    let canViewLandingPageAnalytics: Bool
    let isAdministrator: Bool
    let canOverrideFeatureFlags: Bool
    let isSiteModerator: Bool
    let isCustomerSupport: Bool
    let canCastPublicVotes: Bool
    let hideDownCount: Bool
    let turnstileSiteKey: String?
    let featureFlags: FeatureFlagState?
    let notificationA11yActivator: NotificationA11yActivator?
    let nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator?
    let notificationActivationToken: Int
    let onNavigateToTargetPath: (String) -> Void
    let showSignIn: () -> Void
    @State
    var viewModel: NativeRouteSurfaceViewModel
    @State
    var reviewQueueViewModel: NativeReviewQueueViewModel

    var body: some View {
        Group {
            if isSignedOutChatOrSupportRoute {
                signedOutChatSupportContent
            } else if entry.destinationIdentifier == .chat {
                NativeChatSurface(client: viewModel.client, routeMatch: routeMatch)
            } else if isOwnerLandingPageManagementRoute {
                landingPageManagementContent
            } else if isSignedInMessagesRoute {
                signedInMessagesContent
            } else if isMembershipGrantRoute || isUserAdminRoute {
                membershipGrantOrUserAdminContent
            } else if entry.destinationIdentifier == .engineeringQueues {
                NativeEngineeringQueuesSurface(entry: entry, client: viewModel.client)
                    .id(viewModel.client != nil)
            } else if entry.destinationIdentifier == .engineeringPostgresql {
                NativeEngineeringPostgresqlSurface(entry: entry, client: viewModel.client)
                    .id(viewModel.client != nil)
            } else if entry.destinationIdentifier == .engineeringValkey {
                NativeEngineeringValkeySurface(entry: entry, client: viewModel.client)
                    .id(viewModel.client != nil)
            } else if entry.destinationIdentifier == .engineeringAiCosts {
                NativeAiCostsSurface(entry: entry, client: viewModel.client, isAdministrator: isAdministrator)
                    .id("\(viewModel.client != nil)|\(isAdministrator)")
            } else if entry.destinationIdentifier == .engineeringDynamicConfig, let featureFlags {
                NativeDynamicConfigRouteSurface(client: viewModel.client, featureFlags: featureFlags)
            } else if isDedicatedMemberAppealsRoute {
                memberAppealsContent
            } else if isDedicatedStaffAppealsRoute {
                ModerationAppealsSurface(
                    client: viewModel.client,
                    isSignedIn: isSignedIn,
                    isAdministrator: isAdministrator,
                    isSiteModerator: isSiteModerator
                )
            } else if isDedicatedStaffDisputesRoute {
                ReviewDisputesSurface(
                    client: reviewDisputesClient,
                    isSignedIn: isSignedIn,
                    isAdministrator: isAdministrator,
                    isSiteModerator: isSiteModerator
                )
                .id(reviewDisputesSurfaceIdentity)
            } else if isDedicatedIntegrityRoute {
                integrityRouteContent
            } else {
                scrollContent
            }
        }
        .onChange(of: isSignedIn) { _, _ in rebuildViewModels() }
        .onChange(of: isAdministrator) { _, _ in rebuildViewModels() }
        .onReceive(NotificationCenter.default.publisher(for: .vouchaMembershipEntitlementDidChange)) { _ in
            Task { await viewModel.reloadAfterMembershipEntitlementChange() }
        }
        .task(id: authLoadTaskId) {
            guard shouldLoadRouteSurfaceContent else { return }
            await viewModel.load()
        }
        .emailVerificationRecovery(
            client: viewModel.client,
            gate: viewModel.emailVerificationGate
        )
    }

}

extension NativeRouteDestinationSurface {
    init(
        entry: NativeRouteCatalogEntry,
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        routeQuery: String?,
        playbackController: PodcastPlaybackController? = nil,
        isSignedIn: Bool,
        currentUserId: String? = nil,
        canViewLandingPageAnalytics: Bool = false,
        isAdministrator: Bool = false,
        canOverrideFeatureFlags: Bool = false,
        isSiteModerator: Bool = false,
        isCustomerSupport: Bool = false,
        canCastPublicVotes: Bool = false,
        hideDownCount: Bool = false,
        turnstileSiteKey: String? = nil,
        featureFlags: FeatureFlagState? = nil,
        nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator? = nil,
        a11yActivator: NotificationA11yActivator? = nil,
        notificationActivationToken: Int = 0,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        showSignIn: @escaping () -> Void
    ) {
        self.entry = entry
        self.client = client
        self.routeMatch = routeMatch
        self.routeQuery = routeQuery
        self.playbackController = playbackController
        self.isSignedIn = isSignedIn
        self.currentUserId = currentUserId
        self.canViewLandingPageAnalytics = canViewLandingPageAnalytics
        self.isAdministrator = isAdministrator
        self.canOverrideFeatureFlags = canOverrideFeatureFlags
        self.isSiteModerator = isSiteModerator
        self.isCustomerSupport = isCustomerSupport
        self.canCastPublicVotes = canCastPublicVotes
        self.hideDownCount = hideDownCount
        self.turnstileSiteKey = turnstileSiteKey
        self.featureFlags = featureFlags
        self.nativeOAuthAuthorizationCoordinator = nativeOAuthAuthorizationCoordinator
        notificationA11yActivator = a11yActivator
        self.notificationActivationToken = notificationActivationToken
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.showSignIn = showSignIn
        let resolvedClient = Self.resolvedClient(entry: entry, client: client, isSignedIn: isSignedIn)
        _viewModel = State(
            initialValue: NativeRouteSurfaceViewModel(
                entry: entry,
                client: resolvedClient,
                routeMatch: routeMatch,
                routeQuery: routeQuery,
                isAdministrator: isAdministrator
            )
        )
        _reviewQueueViewModel = State(
            initialValue: NativeReviewQueueViewModel(
                client: resolvedClient,
                isSignedIn: isSignedIn,
                isAdministrator: isAdministrator
            )
        )
    }
}
