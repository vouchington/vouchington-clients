import SwiftUI
import VouchaAPI
import VouchaAuth
import VouchaDesignSystem
import VouchaLocalization

public struct NativeRouteDestinationView: View {
    @Environment(\.locale)
    private var nativeUiLocale
    public let entry: NativeRouteCatalogEntry
    private let client: APIClient?
    private let routeMatch: NativeRouteMatch?
    private let routeQuery: String?
    private let playbackController: PodcastPlaybackController?
    private let isSignedIn: Bool
    private let currentUserId: String?
    private let membershipPlan: String?
    private let isAdministrator: Bool
    private let canOverrideFeatureFlags: Bool
    private let isSiteModerator: Bool
    private let isCustomerSupport: Bool
    private let canCastPublicVotes: Bool
    private let hideDownCount: Bool
    private let turnstileSiteKey: String?
    private let featureFlags: FeatureFlagState?
    private let notificationA11yActivator: NotificationA11yActivator?
    private let nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator?
    private let onNavigateToTargetPath: (String) -> Void
    private let showSignIn: () -> Void
    private let notificationActivationToken: Int

    public init(
        entry: NativeRouteCatalogEntry,
        client: APIClient? = nil,
        routeMatch: NativeRouteMatch? = nil,
        routeQuery: String? = nil,
        playbackController: PodcastPlaybackController? = nil,
        isSignedIn: Bool = true,
        currentUserId: String? = nil,
        membershipPlan: String? = nil,
        isAdministrator: Bool = false,
        canOverrideFeatureFlags: Bool = false,
        isSiteModerator: Bool = false,
        isCustomerSupport: Bool = false,
        turnstileSiteKey: String? = nil,
        featureFlags: FeatureFlagState? = nil,
        nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator? = nil,
        a11yActivator: NotificationA11yActivator? = nil,
        notificationActivationToken: Int = 0,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        showSignIn: @escaping () -> Void = {},
        canCastPublicVotes: Bool = false,
        hideDownCount: Bool = false
    ) {
        self.entry = entry
        self.client = client
        self.routeMatch = routeMatch
        self.routeQuery = routeQuery
        self.playbackController = playbackController
        self.isSignedIn = isSignedIn
        self.currentUserId = currentUserId
        self.membershipPlan = membershipPlan
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
    }

    public var body: some View {
        if let familyText = entry.presentationFamilyText {
            destinationContent
                .navigationTitle(UiMessages.string(familyText, locale: nativeUiLocale))
        } else {
            destinationContent
        }
    }

    private var destinationContent: some View {
        Group {
            if entry.destinationIdentifier == .household {
                HouseholdRouteSurface(client: client, currentUserId: currentUserId)
            } else if entry.destinationIdentifier == .friendRecommendations {
                FriendRecommendationsRouteSurface(
                    client: client,
                    isSignedIn: isSignedIn,
                    onNavigateToTargetPath: onNavigateToTargetPath,
                    showSignIn: showSignIn
                )
            } else if entry.destinationIdentifier == .paymentCards {
                PaymentCardsRouteSurface(client: client, isSignedIn: isSignedIn, showSignIn: showSignIn)
            } else if entry.destinationIdentifier == .pointValuations {
                PointValuationsRouteSurface(client: client, isSignedIn: isSignedIn, showSignIn: showSignIn)
            } else if entry.destinationIdentifier == .spendingCategories {
                SpendingCategoriesRouteSurface(client: client, isSignedIn: isSignedIn, showSignIn: showSignIn)
            } else if entry.destinationIdentifier == .rewardsProgramStatuses {
                RewardsProgramStatusesRouteSurface(client: client, isSignedIn: isSignedIn, showSignIn: showSignIn)
            } else if entry.destinationIdentifier?.requiresAuthenticatedSession == true, !isSignedIn {
                EmptyStateView(
                    icon: "lock",
                    title: .message(.nativeSwiftEmptyStateSignInRequired),
                    message: .message(.nativeAuthSignInToContinue),
                    actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                    action: showSignIn
                )
            } else if let route = ImportExportRoute(path: routeMatch?.path ?? entry.representativePath),
                      let client {
                ImportExportView(route: route, client: client)
            } else if entry.destinationIdentifier == .lists {
                ListsRouteSurface(client: client, isSignedIn: isSignedIn, showSignIn: showSignIn)
            } else {
                NativeRouteDestinationSurface(
                    entry: entry,
                    client: client,
                    routeMatch: routeMatch,
                    routeQuery: routeQuery,
                    playbackController: playbackController,
                    isSignedIn: isSignedIn,
                    currentUserId: currentUserId,
                    canViewLandingPageAnalytics: ["plus", "pro"].contains(membershipPlan?.lowercased()),
                    isAdministrator: isAdministrator,
                    canOverrideFeatureFlags: canOverrideFeatureFlags,
                    isSiteModerator: isSiteModerator,
                    isCustomerSupport: isCustomerSupport,
                    canCastPublicVotes: canCastPublicVotes,
                    hideDownCount: hideDownCount,
                    turnstileSiteKey: turnstileSiteKey,
                    featureFlags: featureFlags,
                    nativeOAuthAuthorizationCoordinator: nativeOAuthAuthorizationCoordinator,
                    a11yActivator: notificationA11yActivator,
                    notificationActivationToken: notificationActivationToken,
                    onNavigateToTargetPath: onNavigateToTargetPath,
                    showSignIn: showSignIn
                )
            }
        }
        .id(routeIdentity)
    }
}

extension NativeRouteDestinationView {
    var routeIdentity: String {
        [
            entry.destinationIdentifier?.rawValue ?? entry.auditFamily,
            routeMatch?.path,
            routeQuery,
            String(isSignedIn),
            currentUserId,
            membershipPlan,
            String(isAdministrator),
            String(canOverrideFeatureFlags),
            String(isSiteModerator),
            String(isCustomerSupport),
            String(canCastPublicVotes),
            String(hideDownCount),
            client.map { String(describing: ObjectIdentifier($0)) }
        ].compactMap { $0 }.joined(separator: "|")
    }
}
