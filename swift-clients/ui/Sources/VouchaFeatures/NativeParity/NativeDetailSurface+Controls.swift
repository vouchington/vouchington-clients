import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeDetailSurface {
    @ViewBuilder
    var hostnameVoteControls: some View {
        if entry.destinationIdentifier == .domainDetail,
           let election = viewModel.hostnameVoteSummary() {
            HStack(spacing: Spacing.sm) {
                VoteControls(
                    election: election,
                    myVote: election.myVote,
                    policy: .sentiment,
                    canCreateVote: canCastPublicVotes && !viewModel.hostnameVoteInFlight,
                    onVote: isSignedIn && !viewModel.hostnameVoteInFlight
                        ? { choice in Task { await viewModel.voteHostname(choice: choice) } }
                        : nil,
                    onSignedOutTap: isSignedIn ? nil : showSignIn
                )
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Spacing.md)
            .padding(.vertical, Spacing.sm)
            .background(Colors.background.opacity(0.85))
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
    }

    @ViewBuilder
    var topicVoteControls: some View {
        if let topicId = viewModel.topicDetailId,
           let election = viewModel.topicVoteSummary(for: topicId) {
            HStack(spacing: Spacing.sm) {
                VoteControls(
                    election: election,
                    myVote: election.myVote,
                    policy: .sentiment,
                    canCreateVote: canCastPublicVotes,
                    onVote: isSignedIn
                        ? { choice in Task { await viewModel.vote(topicId: topicId, choice: choice) } }
                        : nil,
                    onSignedOutTap: isSignedIn ? nil : showSignIn
                )
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Spacing.md)
            .padding(.vertical, Spacing.sm)
            .background(Colors.background.opacity(0.85))
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
    }

    @ViewBuilder
    var userTagControls: some View {
        if isSignedIn, viewModel.detailRelationEntityType == "user", !viewModel.detailRelationIsSelfProfile {
            let canCreateUserTagVote = nativeCanCreateRelationVote(
                isSignedIn: isSignedIn,
                canCastPublicVotes: canCastPublicVotes,
                isAdministrator: viewModel.isAdministrator,
                isUserTag: true
            )
            VStack(alignment: .leading, spacing: Spacing.sm) {
                HStack {
                    Text(UiMessages.string(.nativeSwiftDetailUserTags, locale: nativeUiLocale))
                        .font(Typography.subheadline.bold())
                    Spacer()
                    Button(UiMessages.string(.nativeSwiftCommonManage, locale: nativeUiLocale)) {
                        showingUserTags = true
                    }.buttonStyle(.bordered)
                }
                ForEach(viewModel.detailUserTags) { relation in
                    let myVote = viewModel.detailUserTagVotes[relation.id]?.choice
                    HStack {
                        Text(relation.objectData.displayTitle ?? relation.id)
                        Spacer()
                        Text(UiMessages.number(relation.votesCountUp ?? 0, locale: nativeUiLocale))
                        Text(UiMessages.number(relation.votesCountDown ?? 0, locale: nativeUiLocale))
                        Menu {
                            if canCreateUserTagVote {
                                Button(UiMessages.string(.extractedVotesSemanticVoteConfirm, locale: nativeUiLocale)) {
                                    Task { await viewModel.voteUserTag(relationId: relation.id, choice: .confirm) }
                                }
                                Button(UiMessages.string(.extractedVotesSemanticVoteDispute, locale: nativeUiLocale)) {
                                    Task { await viewModel.voteUserTag(relationId: relation.id, choice: .dispute) }
                                }
                            }
                            if myVote != nil {
                                if canCreateUserTagVote {
                                    Divider()
                                }
                                Button(
                                    UiMessages.string(.extractedVotesSemanticVoteClear, locale: nativeUiLocale),
                                    role: .destructive
                                ) {
                                    Task { await viewModel.voteUserTag(relationId: relation.id, choice: nil) }
                                }
                            }
                        } label: { Image(systemName: "hand.thumbsup").frame(minWidth: 44, minHeight: 44) }
                            .disabled(viewModel
                                .isVotingUserTag(relationId: relation.id) || (!canCreateUserTagVote && myVote == nil))
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.detailUserTagPagination.hasLoadedPage
                        && viewModel.detailUserTagPagination.hasMore,
                    isLoading: viewModel.detailUserTagPagination.isLoading,
                    hasError: viewModel.detailUserTagPagination.lastError != nil,
                    accessibilityIdentifier: "profile-user-tags-pagination"
                ) {
                    await viewModel.loadMoreUserTags()
                }
            }
        }
    }

    var reportErrorBinding: Binding<Bool> {
        Binding(
            get: { viewModel.detailReportErrorMessage != nil },
            set: {
                if !$0 {
                    viewModel.detailReportErrorMessage = nil
                }
            }
        )
    }

    var reportTurnstileBinding: Binding<Bool> {
        Binding(
            get: { viewModel.detailShowingReportTurnstile },
            set: { viewModel.detailShowingReportTurnstile = $0 }
        )
    }

    var reportSuccessBinding: Binding<Bool> {
        Binding(
            get: { viewModel.detailReportSuccessPresented },
            set: { viewModel.detailReportSuccessPresented = $0 }
        )
    }

    func relationButton(
        _ predicate: String,
        activeTitle: UiMessageKey,
        inactiveTitle: UiMessageKey,
        icon: String,
        activeIcon: String
    ) -> some View {
        let active = viewModel.isDetailRelationActive(predicate)
        return Button {
            performSignedIn { await viewModel.toggleDetailRelation(predicate: predicate) }
        } label: {
            Label(
                UiMessages.string(active ? activeTitle : inactiveTitle, locale: nativeUiLocale),
                systemImage: active ? activeIcon : icon
            )
            .font(Typography.caption)
        }
        .buttonStyle(.bordered)
        .tint(.secondary)
        .disabled(viewModel.isLoading)
    }

    func performSignedIn(_ action: @escaping () async -> Void) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        Task { await action() }
    }
}
