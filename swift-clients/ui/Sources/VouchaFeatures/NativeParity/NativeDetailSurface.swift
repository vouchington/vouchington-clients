import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct NativeDetailSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel
    var isSignedIn = true
    var canCastPublicVotes = false
    var turnstileSiteKey: String?
    var showSignIn: () -> Void = {}
    var onNavigate: ((String) -> Void)?
    var showsRows = true
    @State
    var showingReportDialog = false
    @State
    var showingReportNoteDialog = false
    @State
    var showingUserTags = false

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if entry.destinationIdentifier == .urlDetail, !isSignedIn {
                EmptyStateView(
                    icon: "lock",
                    title: .message(.nativeSwiftEmptyStateSignInRequired),
                    message: .message(.nativeSwiftEmptyStateSignInUrlDetailsMessage)
                )
            } else {
                topicVoteControls
                hostnameVoteControls
                relationControls
                reportingControls
                userTagControls
                destinationRows
            }
        }
        .confirmationDialog(reportDialogTitle, isPresented: $showingReportDialog, titleVisibility: .visible) {
            ForEach(nativeProfileReportReasons) { reason in
                Button(UiMessages.string(reason.title, locale: nativeUiLocale)) {
                    startReportDetail(reason: reason.value)
                }
            }
        } message: {
            Text(reportDialogMessage)
        }
        .alert(
            UiMessages.string(.nativeSwiftDetailReportFailed, locale: nativeUiLocale),
            isPresented: reportErrorBinding
        ) {
            Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                retryReportDetail()
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                cancelReportDetail()
            }
        } message: {
            Text(
                viewModel.detailReportErrorMessage.map {
                    UiMessages.string($0, locale: nativeUiLocale)
                } ?? ""
            )
        }
        .alert(reportSuccessTitle, isPresented: reportSuccessBinding) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {}
        } message: {
            Text(reportSuccessMessage)
        }
        .sheet(isPresented: reportTurnstileBinding) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                handleReportTurnstileToken(token)
            }
        }
        .sheet(isPresented: $showingReportNoteDialog, onDismiss: presentReportTurnstileAfterNoteDismiss) {
            reportNoteSheet
        }
        .sheet(
            isPresented: $showingUserTags,
            onDismiss: { Task { await viewModel.reloadUserTags() } },
            content: { userTagsSheet }
        )
    }

    var userTagsSheet: some View {
        NativeTagManagementSurface(
            client: viewModel.client,
            routeMatch: nil,
            subjectKind: .user,
            subjectId: viewModel.detailRelationEntityId,
            subjectTitle: viewModel.rows.first?.title,
            isSignedIn: isSignedIn,
            canCreateVote: nativeCanCreateRelationVote(
                isSignedIn: isSignedIn,
                canCastPublicVotes: canCastPublicVotes,
                isAdministrator: viewModel.isAdministrator,
                isUserTag: true
            ),
            showSignIn: showSignIn,
            onNavigate: onNavigate ?? { _ in }
        )
        .padding()
    }

    @ViewBuilder
    private var relationControls: some View {
        if let entityType = viewModel.detailRelationEntityType,
           let entityId = viewModel.detailRelationEntityId {
            let isUserProfile = entityType == "user"
            if !isUserProfile || !viewModel.detailRelationIsSelfProfile {
                HStack(spacing: Spacing.xs) {
                    relationButton(
                        "follow",
                        activeTitle: .nativeSwiftDesignSystemFollowing,
                        inactiveTitle: .nativeSwiftDesignSystemFollow,
                        icon: "plus",
                        activeIcon: "checkmark"
                    )
                    if !isUserProfile || isSignedIn {
                        relationButton(
                            "mute",
                            activeTitle: .nativeSwiftDesignSystemMuted,
                            inactiveTitle: .nativeSwiftDesignSystemMute,
                            icon: "speaker.slash",
                            activeIcon: "speaker.slash.fill"
                        )
                    }
                    if isUserProfile, isSignedIn {
                        relationButton(
                            "block",
                            activeTitle: .nativeSwiftRouteSurfaceBlocked,
                            inactiveTitle: .nativeSwiftRouteSurfaceBlock,
                            icon: "hand.raised",
                            activeIcon: "hand.raised.fill"
                        )
                    }
                    Spacer(minLength: 0)
                }
                .padding(.horizontal, Spacing.md)
                .padding(.vertical, Spacing.sm)
                .background(Colors.background.opacity(0.85))
                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                .disabled(viewModel.isLoading)
                .opacity(entityId.isEmpty ? 0 : 1)
            }
        }
    }

}
