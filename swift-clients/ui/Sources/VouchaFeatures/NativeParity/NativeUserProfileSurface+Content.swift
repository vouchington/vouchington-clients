import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeUserProfileSurface {
    func profileHeader(_ profile: UserProfileResponse) -> some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            Avatar(
                imageURL: AppConfig.shared.imageURL(forImageId: profile.user.profileImageId, width: 144),
                username: profile.user.username ?? "",
                size: 72
            )
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(verbatim: profile.user.displayAccount?.name
                    ?? profile.user.verifiedDisplayName
                    ?? profile.user.username
                    ?? profile.user.id)
                    .font(Typography.headline).bold()
                AccountTypeBadge(accountType: profile.user.accountType)
                if let username = profile.user.username?.ifNotEmpty {
                    Text(verbatim: UiMessages.string(.userContent("@\(username)"), locale: nativeUiLocale))
                        .foregroundStyle(Colors.secondaryLabel)
                }
                NativeHtmlContent(html: profile.userBioHtml, fallback: profile.user.markdown)
                profileCounts(profile)
                trustControls(profile)
                trustContextGroups
                profileLinks(profile)
                if isAdministrator {
                    Button(UiMessages.string(
                        .message(.nativeSwiftIdentityVerificationAdmin),
                        locale: nativeUiLocale
                    )) {
                        let target = profile.user.username?.ifNotEmpty ?? profile.user.id
                        onNavigate(NativeUserProfileNavigationTarget.userAdmin(target))
                    }
                    .accessibilityIdentifier("user-profile-admin")
                }
            }
        }
        .accessibilityIdentifier("public-profile-header")
    }

    @ViewBuilder
    func trustControls(_ profile: UserProfileResponse) -> some View {
        if !viewModel.detailRelationIsSelfProfile,
           !isSignedIn || canCastPublicVotes || viewModel.userProfile.trustChoice != nil {
            VoteControls(
                election: nil,
                myVote: viewModel.userProfile.trustChoice,
                policy: .sentiment,
                canCreateVote: isSignedIn && canCastPublicVotes && !viewModel.userProfile.trustVoteInFlight,
                onVote: isSignedIn && !viewModel.userProfile.trustVoteInFlight
                    ? { choice in Task { await viewModel.voteUserTrust(userId: profile.user.id, choice: choice) } }
                    : nil,
                onSignedOutTap: isSignedIn ? nil : showSignIn
            )
            .accessibilityIdentifier("public-profile-trust-vote")
        }
    }

    @ViewBuilder
    private var trustContextGroups: some View {
        if let context = viewModel.userProfile.trustContext {
            trustContextGroup(
                context.positiveByFollowing,
                title: .nativeSwiftProfilePositiveSignalsFromPeopleYouFollow,
                accessibilityIdentifier: "public-profile-positive-trust-context"
            )
            trustContextGroup(
                context.negativeByFollowing,
                title: .nativeSwiftProfileNegativeSignalsFromPeopleYouFollow,
                accessibilityIdentifier: "public-profile-negative-trust-context"
            )
        }
    }

    private func trustContextGroup(
        _ group: UserTrustContextGroup,
        title: UiMessageKey,
        accessibilityIdentifier: String
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            localizedCount(title, group.total).font(Typography.caption).foregroundStyle(Colors.secondaryLabel)
            ForEach(group.users) { user in
                UserRow(
                    user: user,
                    avatarURL: AppConfig.shared.imageURL(forImageId: user.profileImageId, width: 96)
                )
            }
        }
        .accessibilityIdentifier(accessibilityIdentifier)
    }

    func profileLinks(_ profile: UserProfileResponse) -> some View {
        HStack(spacing: Spacing.sm) {
            ForEach(profile.profileLinks) { link in
                if let url = resolvedProfileLinkURL(link) {
                    Link(
                        UiMessages
                            .string(
                                (link.name ?? link.handle ?? url.host)
                                    .map(UiVerbatimText.userContent) ?? .message(.nativeSwiftRouteSurfaceLink),
                                locale: nativeUiLocale
                            ),
                        destination: url
                    )
                }
            }
        }
        .font(Typography.caption)
    }

    func profileCounts(_ profile: UserProfileResponse) -> some View {
        let metrics = profile.userMetrics
        let posts = ["reviews", "discussions", "comments"].reduce(0) { $0 + (metrics?.visibleCount($1) ?? 0) }
        return HStack(spacing: Spacing.md) {
            localizedCount(.nativeSwiftProfilePostsCount, posts)
            localizedCount(.nativeSwiftProfileFollowingCount, metrics?.visibleCount("users_following") ?? 0)
            localizedCount(.nativeSwiftProfileFollowersCount, metrics?.visibleCount("users_followers") ?? 0)
        }
        .font(Typography.caption)
        .foregroundStyle(Colors.secondaryLabel)
    }

    func primaryTabs(_ profile: UserProfileResponse) -> some View {
        ScrollView(.horizontal) {
            HStack(spacing: Spacing.xs) {
                ForEach(visiblePrimaryTabs(profile), id: \.title) { tab in
                    tabButton(tab.title, path: tab.path, selected: tab.title == currentScope?.primaryTitle)
                }
            }
        }
    }

    @ViewBuilder var contextualTabs: some View {
        if let scope = currentScope {
            HStack(spacing: Spacing.xs) {
                ForEach(contextualTabs(for: scope, profile: viewModel.userProfile.header), id: \.title) { tab in
                    tabButton(tab.title, path: tab.path, selected: tab.selected)
                }
            }
        }
    }
}
