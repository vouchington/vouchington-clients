import SwiftUI
import VouchaAPI
import VouchaDesignSystem

public struct NativeRouteFamilyDetailView: View {
    public let group: NativeRouteFamilyGroup
    private let client: APIClient?
    private let isSignedIn: Bool
    private let isAdministrator: Bool
    private let canOverrideFeatureFlags: Bool
    private let isSiteModerator: Bool
    private let canCastPublicVotes: Bool
    private let turnstileSiteKey: String?
    private let featureFlags: FeatureFlagState?
    private let onNavigateToTargetPath: (String) -> Void
    private let showSignIn: () -> Void

    public init(
        group: NativeRouteFamilyGroup,
        client: APIClient? = nil,
        isSignedIn: Bool = true,
        isAdministrator: Bool = false,
        canOverrideFeatureFlags: Bool = false,
        isSiteModerator: Bool = false,
        canCastPublicVotes: Bool = false,
        turnstileSiteKey: String? = nil,
        featureFlags: FeatureFlagState? = nil,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        showSignIn: @escaping () -> Void = {}
    ) {
        self.group = group
        self.client = client
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.canOverrideFeatureFlags = canOverrideFeatureFlags
        self.isSiteModerator = isSiteModerator
        self.canCastPublicVotes = canCastPublicVotes
        self.turnstileSiteKey = turnstileSiteKey
        self.featureFlags = featureFlags
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.showSignIn = showSignIn
    }

    public var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                NativeRouteFamilyCard(group: group)

                ForEach(group.entries, id: \.representativePath) { entry in
                    NavigationLink {
                        NativeRouteDestinationView(
                            entry: entry,
                            client: client,
                            routeMatch: NativeRouteCatalog.matchingRoute(for: entry.representativePath)?.match,
                            isSignedIn: isSignedIn,
                            isAdministrator: isAdministrator,
                            canOverrideFeatureFlags: canOverrideFeatureFlags,
                            isSiteModerator: isSiteModerator,
                            turnstileSiteKey: turnstileSiteKey,
                            featureFlags: featureFlags,
                            onNavigateToTargetPath: onNavigateToTargetPath,
                            showSignIn: showSignIn,
                            canCastPublicVotes: canCastPublicVotes
                        )
                    } label: {
                        NativeRouteDestinationCard(entry: entry)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(Spacing.md)
        }
    }
}
