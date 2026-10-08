import SwiftUI

func nativeCanCreateRelationVote(
    isSignedIn: Bool,
    canCastPublicVotes: Bool,
    isAdministrator: Bool,
    isUserTag: Bool
) -> Bool {
    isSignedIn && (!isUserTag || canCastPublicVotes || isAdministrator)
}

extension NativeRouteDestinationSurface {
    var communitySurface: some View {
        CommunitySurface(
            client: viewModel.client,
            routeMatch: viewModel.routeMatch,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator,
            turnstileSiteKey: resolvedTurnstileSiteKey,
            showSignIn: showSignIn,
            onNavigate: onNavigateToTargetPath
        )
    }

    @ViewBuilder
    var topicRecommendationContent: some View {
        if topicRecommendationFormMode != nil || viewModel.routeMatch?.param("id") != nil {
            NativeTopicRecommendationSurface(
                client: viewModel.client,
                recommendationId: viewModel.routeMatch?.param("id"),
                turnstileSiteKey: resolvedTurnstileSiteKey,
                canCreateVote: canCastPublicVotes,
                isForm: topicRecommendationFormMode != nil,
                isSignedIn: isSignedIn,
                showSignIn: showSignIn,
                hideDownCount: hideDownCount
            )
        } else {
            NativeTopicRecommendationsRootSurface(
                routeViewModel: viewModel,
                isSignedIn: isSignedIn,
                isAdministrator: isAdministrator,
                onNavigateToTargetPath: onNavigateToTargetPath
            )
        }
    }

    @ViewBuilder
    var postDetailContent: some View {
        if isTagManagementRoute {
            tagManagementSurface
        } else {
            NativeCommentThreadSurface(
                client: viewModel.client,
                routeMatch: viewModel.routeMatch,
                currentUserId: currentUserId,
                isSignedIn: isSignedIn,
                canCreateVote: canCastPublicVotes,
                hideDownCount: hideDownCount,
                turnstileSiteKey: resolvedTurnstileSiteKey,
                showSignIn: showSignIn
            )
        }
    }

    @ViewBuilder
    var detailDestinationContent: some View {
        if isTagManagementRoute {
            tagManagementSurface
        } else {
            NativeDetailSurface(
                entry: entry,
                viewModel: viewModel,
                isSignedIn: isSignedIn,
                canCastPublicVotes: canCastPublicVotes,
                turnstileSiteKey: resolvedTurnstileSiteKey,
                showSignIn: showSignIn,
                onNavigate: onNavigateToTargetPath
            )
        }
    }

    var userProfileContent: some View {
        NativeUserProfileSurface(
            entry: entry,
            viewModel: viewModel,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            canCastPublicVotes: canCastPublicVotes,
            hideDownCount: hideDownCount,
            turnstileSiteKey: resolvedTurnstileSiteKey,
            showSignIn: showSignIn,
            onNavigate: onNavigateToTargetPath
        )
    }

    var tagManagementSurface: some View {
        NativeTagManagementSurface(
            client: viewModel.client,
            routeMatch: viewModel.routeMatch,
            subjectKind: tagManagementSubjectKind,
            isSignedIn: isSignedIn,
            canCreateVote: nativeCanCreateRelationVote(
                isSignedIn: isSignedIn,
                canCastPublicVotes: canCastPublicVotes,
                isAdministrator: isAdministrator,
                isUserTag: tagManagementSubjectKind == .user
            ),
            showSignIn: showSignIn,
            onNavigate: onNavigateToTargetPath
        )
    }
}
