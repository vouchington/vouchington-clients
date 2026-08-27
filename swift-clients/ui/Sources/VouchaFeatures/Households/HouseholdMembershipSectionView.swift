import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct HouseholdMembershipSectionView: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let section: HouseholdMembershipSection
    let removingMembershipIds: Set<String>
    let loadNextPage: () async -> Void
    let remove: (HouseholdMembership) async -> Void
    @State
    private var pendingRemoval: HouseholdMembership?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Text(UiMessages.string(
                    section.isOwned
                        ? .nativeSwiftHouseholdsBookmarksYourHousehold
                        : .nativeSwiftHouseholdsBookmarksHouseholdYouBelongTo,
                    locale: nativeUiLocale
                ))
                .font(Typography.headline)
                Spacer()
                if !section.isOwned {
                    Text(UiMessages.string(
                        .nativeSwiftHouseholdsBookmarksReadOnly,
                        locale: nativeUiLocale
                    )).foregroundStyle(Colors.secondaryLabel)
                }
            }

            if section.isLoading, section.members.isEmpty {
                ProgressView(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksLoadingMembers,
                    locale: nativeUiLocale
                ))
            } else if let error = section.errorMessage {
                Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
            }

            if !section.members.isEmpty {
                ForEach(section.members) { membership in memberRow(membership) }
            } else if !section.isLoading, section.errorMessage == nil {
                Text(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksNoHouseholdMembers,
                    locale: nativeUiLocale
                )).foregroundStyle(Colors.secondaryLabel)
            }

            HybridPaginationControl(
                hasMore: section.hasMore,
                isLoading: section.isLoading,
                hasError: section.hasPaginationError
            ) {
                await loadNextPage()
            }
            .accessibilityLabel(UiMessages.string(
                section.hasPaginationError
                    ? .nativeSwiftHouseholdsBookmarksRetryMembers
                    : .nativeSwiftCommonLoadMore,
                locale: nativeUiLocale
            ))
        }
        .confirmationDialog(
            UiMessages.string(.nativeSwiftHouseholdsBookmarksRemoveMemberConfirmation, locale: nativeUiLocale),
            isPresented: Binding(
                get: { pendingRemoval != nil },
                set: {
                    if !$0 {
                        pendingRemoval = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button(UiMessages.string(
                .nativeSwiftHouseholdsBookmarksRemoveMember,
                locale: nativeUiLocale
            ), role: .destructive) {
                guard let membership = pendingRemoval else { return }
                pendingRemoval = nil
                Task { await remove(membership) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                pendingRemoval = nil
            }
        }
    }

    private func memberRow(_ membership: HouseholdMembership) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(membership.householdDisplayName, locale: nativeUiLocale))
                if let relationship = membership.householdDisplayRelationship {
                    Text(UiMessages.string(relationship, locale: nativeUiLocale))
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
            Spacer()
            if section.isOwned {
                Button(UiMessages.string(
                    .nativeSwiftHouseholdsBookmarksRemove,
                    locale: nativeUiLocale
                )) { pendingRemoval = membership }
                    .disabled(removingMembershipIds.contains(membership.id))
                    .accessibilityLabel(UiMessages.string(
                        .nativeSwiftHouseholdsBookmarksRemoveMemberAccessibility,
                        parameters: [
                            "name": UiMessages.string(membership.householdDisplayName, locale: nativeUiLocale)
                        ],
                        locale: nativeUiLocale
                    ))
            }
        }
    }
}
