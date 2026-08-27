import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityMemberControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var memberUserId = ""
    @State
    private var transferUserId = ""
    @State
    private var memberRole: CommunityMemberRole = .member

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesMembers, locale: nativeUiLocale)).font(Typography.headline)
            TextField(UiMessages.string(.nativeSwiftCommunitiesUserId, locale: nativeUiLocale), text: $memberUserId)
                .textFieldStyle(.roundedBorder)
            Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale), systemImage: "minus.circle") {
                guard isSignedIn else { showSignIn()
                    return
                }
                Task { await viewModel.removeMember(userId: memberUserId) }
            }
            if canManageMemberRoles {
                Picker(UiMessages.string(.nativeSwiftCommunitiesRole, locale: nativeUiLocale), selection: $memberRole) {
                    ForEach(memberRoles, id: \.self) { role in
                        Text(UiMessages.string(role.titleKey, locale: nativeUiLocale)).tag(role)
                    }
                }
                .pickerStyle(.segmented)
                Button(
                    UiMessages.string(.nativeSwiftCommunitiesUpdateRole, locale: nativeUiLocale),
                    systemImage: "person.badge.gearshape"
                ) {
                    guard isSignedIn else { showSignIn()
                        return
                    }
                    Task { await viewModel.updateMemberRole(userId: memberUserId, role: memberRole) }
                }
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesTransferToUserId, locale: nativeUiLocale),
                        text: $transferUserId
                    ).textFieldStyle(.roundedBorder)
                    Button(
                        UiMessages.string(.nativeSwiftCommunitiesTransferOwnership, locale: nativeUiLocale),
                        systemImage: "person.crop.circle.badge.checkmark"
                    ) {
                        guard isSignedIn else { showSignIn()
                            return
                        }
                        Task { await viewModel.transferOwnership(userId: transferUserId) }
                    }
                }
            }
        }
    }

    private var memberRoles: [CommunityMemberRole] {
        [.owner, .moderator, .member]
    }

    private var canManageMemberRoles: Bool {
        viewModel.isAdministrator || viewModel.communityDetail?.membership?.role == .owner
    }
}

struct CommunityListItemControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let listItemType: CommunityListItemType
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var entityId = ""
    @State
    private var itemId = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(listItemType.titleKey, locale: nativeUiLocale))
                .font(Typography.headline)
            HStack {
                TextField(UiMessages.string(.nativeSwiftCommunitiesEntityId, locale: nativeUiLocale), text: $entityId)
                    .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommonAdd, locale: nativeUiLocale), systemImage: "plus.circle") {
                    guard isSignedIn else { showSignIn()
                        return
                    }
                    Task { await viewModel.addListItem(itemType: listItemType, entityId: entityId) }
                }
            }
            HStack {
                TextField(UiMessages.string(.nativeSwiftCommunitiesItemId, locale: nativeUiLocale), text: $itemId)
                    .textFieldStyle(.roundedBorder)
                Button(
                    UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale),
                    systemImage: "minus.circle"
                ) {
                    guard isSignedIn else { showSignIn()
                        return
                    }
                    Task { await viewModel.removeListItem(itemType: listItemType, itemId: itemId) }
                }
            }
        }
    }
}

struct CommunityPinnedPostControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var pinnedPostIds = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesPinnedPosts, locale: nativeUiLocale))
                .font(Typography.headline)
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesCommaSeparatedPostIds, locale: nativeUiLocale),
                text: $pinnedPostIds
            )
            .textFieldStyle(.roundedBorder)
            Button(
                UiMessages.string(.nativeSwiftCommunitiesUpdatePinnedPosts, locale: nativeUiLocale),
                systemImage: "pin"
            ) {
                guard isSignedIn else { showSignIn()
                    return
                }
                let postIds = pinnedPostIds.split(separator: ",").map { String($0) }
                Task { await viewModel.updatePinnedPosts(postIds: postIds) }
            }
        }
    }
}
