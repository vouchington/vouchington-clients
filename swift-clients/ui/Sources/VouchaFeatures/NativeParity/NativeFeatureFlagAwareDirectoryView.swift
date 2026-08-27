import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

public struct NativeFeatureFlagAwareDirectoryView: View {
    private let section: AppSection
    private let client: APIClient?
    private let isSignedIn: Bool
    private let userRoles: [String]
    private let canCastPublicVotes: Bool
    private let turnstileSiteKey: String?
    private let featureFlags: FeatureFlagState
    private let onNavigateToTargetPath: (String) -> Void
    private let showSignIn: () -> Void

    public init(
        section: AppSection,
        client: APIClient? = nil,
        isSignedIn: Bool,
        userRoles: [String],
        canCastPublicVotes: Bool = false,
        turnstileSiteKey: String? = nil,
        featureFlags: FeatureFlagState,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        showSignIn: @escaping () -> Void = {}
    ) {
        self.section = section
        self.client = client
        self.isSignedIn = isSignedIn
        self.userRoles = userRoles
        self.canCastPublicVotes = canCastPublicVotes
        self.turnstileSiteKey = turnstileSiteKey
        self.featureFlags = featureFlags
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.showSignIn = showSignIn
    }

    public var body: some View {
        VStack(spacing: 0) {
            if section == .engineering,
               FeatureFlagState.canManageDeviceOverrides(userRoles: userRoles) {
                NavigationLink {
                    NativeFeatureFlagOverridesSurface(
                        client: client,
                        featureFlags: featureFlags,
                        canOverride: true
                    )
                } label: {
                    NativeFeatureFlagOverridesDirectoryCard()
                }
                .buttonStyle(.plain)
                .padding([.horizontal, .top], Spacing.md)
            }

            NativeRouteFamilyDirectoryView(
                groups: section.nativeParityGroups(
                    isSignedIn: isSignedIn,
                    userRoles: userRoles,
                    featureFlags: featureFlags.effective
                ),
                client: client,
                isSignedIn: isSignedIn,
                isAdministrator: userRoles.contains("administrator"),
                canOverrideFeatureFlags: FeatureFlagState.canManageDeviceOverrides(
                    userRoles: userRoles
                ),
                isSiteModerator: userRoles.contains("moderator"),
                canCastPublicVotes: canCastPublicVotes,
                turnstileSiteKey: turnstileSiteKey,
                featureFlags: featureFlags,
                onNavigateToTargetPath: onNavigateToTargetPath,
                showSignIn: showSignIn
            )
        }
    }
}

private struct NativeFeatureFlagOverridesDirectoryCard: View {
    @Environment(\.locale)
    private var nativeUiLocale

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            Image(systemName: "iphone.gen3.radiowaves.left.and.right")
                .font(.title3)
                .foregroundStyle(Colors.primary)
                .frame(width: 24)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsDeviceFeatureFlags,
                    locale: nativeUiLocale
                )).font(Typography.headline).foregroundStyle(.primary)
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsDeviceFeatureFlagsDescription,
                    locale: nativeUiLocale
                ))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer(minLength: Spacing.sm)
            Image(systemName: "chevron.right")
                .foregroundStyle(Colors.secondaryLabel)
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}
