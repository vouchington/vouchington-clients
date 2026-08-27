import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct HouseholdRouteSurface: View {
    let client: APIClient?
    let currentUserId: String?

    var body: some View {
        if let client, let currentUserId, !currentUserId.isEmpty {
            HouseholdSurface(client: client)
        } else {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftHouseholdsBookmarksSignInHouseholdMessage)
            )
            .padding(Spacing.md)
        }
    }
}

struct HouseholdSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: HouseholdViewModel

    init(client: APIClient) {
        _viewModel = State(initialValue: HouseholdViewModel(
            service: HouseholdService(client: client)
        ))
    }

    init(viewModel: HouseholdViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(UiMessages.string(.nativeSwiftHouseholdsBookmarksHousehold, locale: nativeUiLocale))
                    .font(Typography.largeTitle)
                Text(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksHouseholdDescription,
                    locale: nativeUiLocale
                ))
                .foregroundStyle(Colors.secondaryLabel)

                if viewModel.isLoading, viewModel.sections.isEmpty {
                    ProgressView(UiMessages.string(
                        .nativeSwiftHouseholdsBookmarksLoadingHousehold,
                        locale: nativeUiLocale
                    ))
                } else {
                    if let error = viewModel.errorMessage {
                        householdLoadError(error)
                    }
                    ForEach(viewModel.sections) { section in
                        HouseholdMembershipSectionView(
                            section: section,
                            removingMembershipIds: viewModel.removingMembershipIds,
                            loadNextPage: { await viewModel.loadNextMemberships(householdId: section.id) },
                            remove: { await viewModel.removeMembership($0, householdId: section.id) }
                        )
                    }
                    HybridPaginationControl(
                        hasMore: viewModel.hasMoreMemberHouseholds,
                        isLoading: viewModel.isLoadingMemberHouseholds,
                        hasError: viewModel.hasMemberHouseholdPaginationError
                    ) {
                        await viewModel.loadNextMemberHouseholds()
                    }
                    createSection
                }

                if let error = viewModel.mutationErrorMessage {
                    Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
                }
            }
            .padding(Spacing.md)
        }
        .task { await viewModel.load() }
    }

    private func householdLoadError(_ message: UiVerbatimText) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(message, locale: nativeUiLocale)).foregroundStyle(.red)
            Button(UiMessages.string(
                .nativeSwiftHouseholdsBookmarksRetryHousehold,
                locale: nativeUiLocale
            )) { Task { await viewModel.load() } }
        }
    }

    @ViewBuilder
    private var createSection: some View {
        if viewModel.canCreateHousehold {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                Text(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksYourHousehold,
                    locale: nativeUiLocale
                )).font(Typography.headline)
                Text(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksCreateHouseholdDescription,
                    locale: nativeUiLocale
                ))
                .foregroundStyle(Colors.secondaryLabel)
                Button(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksCreateHousehold,
                    locale: nativeUiLocale
                )) { Task { await viewModel.createHousehold() } }
                    .buttonStyle(.borderedProminent)
            }
        }
    }
}
