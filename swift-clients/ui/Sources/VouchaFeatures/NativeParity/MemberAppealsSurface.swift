import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct MemberAppealsSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: MemberAppealsViewModel
    private let turnstileSiteKey: String?

    init(
        client: APIClient?,
        isSignedIn: Bool,
        currentUserId: String?,
        route: MemberAppealsRoute,
        turnstileSiteKey: String?,
        draftStore: MemberAppealDraftStore? = nil
    ) {
        self.turnstileSiteKey = turnstileSiteKey
        _viewModel = State(initialValue: MemberAppealsViewModel(
            client: client,
            isSignedIn: isSignedIn,
            currentUserId: currentUserId,
            route: route,
            draftStore: draftStore
        ))
    }

    init(viewModel: MemberAppealsViewModel, turnstileSiteKey: String? = nil) {
        self.turnstileSiteKey = turnstileSiteKey
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        if !viewModel.isSignedIn {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftModerationAppealsMemberSignInMessage)
            )
        } else {
            content
                .task { await viewModel.load() }
                .sheet(item: activeTargetBinding) { _ in
                    MemberAppealForm(
                        viewModel: viewModel,
                        turnstileSiteKey: turnstileSiteKey
                    )
                }
        }
    }

    private var content: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                if let errorMessage = viewModel.errorMessage {
                    Text(UiMessages.string(errorMessage, locale: nativeUiLocale))
                        .foregroundStyle(.red)
                        .accessibilityIdentifier("member-appeals-error")
                    Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                        Task { await viewModel.load() }
                    }
                }
                if viewModel.isLoading, viewModel.appeals.isEmpty, viewModel.eligibleTargets.isEmpty {
                    ProgressView(UiMessages.string(
                        .nativeSwiftModerationAppealsMemberLoading,
                        locale: nativeUiLocale
                    ))
                    .frame(maxWidth: .infinity)
                } else {
                    noticesSection
                    if viewModel.route == .tracking {
                        trackingSection
                    }
                }
            }
            .padding(Spacing.md)
        }
    }

    @ViewBuilder
    private var noticesSection: some View {
        if viewModel.eligibleTargets.isEmpty {
            EmptyStateView(
                icon: "checkmark.shield",
                title: .message(.nativeSwiftModerationAppealsMemberNoEligibleNotices),
                message: .message(.nativeSwiftModerationAppealsMemberNoEligibleNoticesMessage)
            )
        } else {
            Text(UiMessages.string(.nativeSwiftModerationAppealsMemberEligibleNotices, locale: nativeUiLocale))
                .font(.title2)
            ForEach(viewModel.eligibleTargets) { target in
                MemberAppealNoticeCard(
                    target: target,
                    canAppeal: viewModel.canAppeal(target)
                ) {
                    viewModel.beginAppeal(target)
                }
            }
        }
        noticePaginationControls
    }

    @ViewBuilder
    private var trackingSection: some View {
        Text(UiMessages.string(.nativeSwiftModerationAppealsMemberYourAppeals, locale: nativeUiLocale))
            .font(.title2)
        if viewModel.appeals.isEmpty {
            EmptyStateView(
                icon: "arrow.uturn.left.circle",
                title: .message(.nativeSwiftModerationAppealsMemberNoAppeals),
                message: .message(.nativeSwiftModerationAppealsMemberNoAppealsMessage)
            )
        } else {
            ForEach(viewModel.appeals) { appeal in
                MemberAppealTrackingCard(appeal: appeal)
            }
            ForEach(ModerationAppealStatus.allCases, id: \.rawValue) { status in
                let pagination = viewModel.appealPagination(status: status)
                HybridPaginationControl(
                    hasMore: pagination.hasMore,
                    isLoading: pagination.isLoading,
                    hasError: pagination.lastError != nil,
                    accessibilityIdentifier: "member-appeals-\(status.rawValue)-pagination"
                ) {
                    await viewModel.loadMoreAppeals(status: status)
                }
            }
        }
        if let error = viewModel.loadMoreErrorMessage {
            Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
        }
    }

    private var noticePaginationControls: some View {
        ForEach(noticePaginationKinds, id: \.id) { item in
            HybridPaginationControl(
                hasMore: item.hasMore,
                isLoading: item.isLoading,
                hasError: item.hasError
            ) {
                await viewModel.loadMoreNotices(item.kind)
            }
        }
    }

    private var noticePaginationKinds: [MemberAppealNoticePaginationItem] {
        [
            .init(kind: .warnings, pagination: viewModel.warningPagination),
            .init(kind: .bans, pagination: viewModel.banPagination),
            .init(kind: .removedPosts, pagination: viewModel.removalPagination)
        ].filter(\.isRelevant)
    }

}

private extension MemberAppealsSurface {
    var activeTargetBinding: Binding<MemberAppealTarget?> {
        Binding(get: { viewModel.activeTarget }, set: {
            if $0 == nil {
                viewModel.cancelAppeal()
            }
        })
    }
}
