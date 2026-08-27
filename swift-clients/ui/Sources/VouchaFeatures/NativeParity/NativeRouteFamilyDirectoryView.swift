import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

public struct NativeRouteFamilyDirectoryView: View {
    @Environment(\.locale)
    var nativeUiLocale
    public let groups: [NativeRouteFamilyGroup]
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
        groups: [NativeRouteFamilyGroup],
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
        self.groups = groups
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
        if groups.isEmpty {
            EmptyStateView(
                icon: "square.grid.2x2",
                title: .message(.nativeSwiftEmptyStateNoNativeDestinations),
                message: .message(.nativeSwiftEmptyStateNoNativeDestinationsMessage)
            )
        } else {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.md) {
                    ForEach(groups) { group in
                        NavigationLink {
                            NativeRouteFamilyDetailView(
                                group: group,
                                client: client,
                                isSignedIn: isSignedIn,
                                isAdministrator: isAdministrator,
                                canOverrideFeatureFlags: canOverrideFeatureFlags,
                                isSiteModerator: isSiteModerator,
                                canCastPublicVotes: canCastPublicVotes,
                                turnstileSiteKey: turnstileSiteKey,
                                featureFlags: featureFlags,
                                onNavigateToTargetPath: onNavigateToTargetPath,
                                showSignIn: showSignIn
                            )
                        } label: {
                            NativeRouteFamilyCard(group: group)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            .padding(Spacing.md)
        }
    }
}
