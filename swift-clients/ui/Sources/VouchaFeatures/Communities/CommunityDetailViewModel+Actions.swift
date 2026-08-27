import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func updateMemberRole(userId: String, role: CommunityMemberRole) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty else { return }
        await perform(
            .updateCommunityMemberRole(idOrSlug: slug, userId: userId, role: role),
            success: UiMessage(.nativeSwiftCommunityStatusUpdatedMember)
        )
    }

    func removeMember(userId: String) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty else { return }
        await perform(
            .removeCommunityMember(idOrSlug: slug, userId: userId),
            success: UiMessage(.nativeSwiftCommunityStatusRemovedMember)
        )
    }

    func transferOwnership(userId: String) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty else { return }
        await perform(
            .transferCommunityOwnership(idOrSlug: slug, userId: userId),
            success: UiMessage(.nativeSwiftCommunityStatusTransferredOwnership)
        )
    }

    func addListItem(itemType: CommunityListItemType, entityId: String) async {
        let entityId = entityId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !entityId.isEmpty else { return }
        let endpoint: Endpoint = switch itemType {
        case .topic:
            .addCommunityListTopic(idOrSlug: slug, topicId: entityId)
        case .rssFeed:
            .addCommunityListRssFeed(idOrSlug: slug, rssFeedId: entityId)
        case .post:
            .addCommunityListPost(idOrSlug: slug, postId: entityId)
        case .urlHostname:
            .addCommunityListDomain(idOrSlug: slug, urlHostnameId: entityId)
        case .url:
            .addCommunityListUrl(idOrSlug: slug, urlId: entityId)
        }
        await perform(endpoint, success: UiMessage(.nativeSwiftCommunityStatusAddedListItem))
    }

    func removeListItem(itemType: CommunityListItemType, itemId: String) async {
        let itemId = itemId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !itemId.isEmpty else { return }
        await perform(
            .removeCommunityListItem(idOrSlug: slug, itemType: itemType, itemId: itemId),
            success: UiMessage(.nativeSwiftCommunityStatusRemovedListItem)
        )
    }

    func approveApplication(applicationId: String) async {
        let applicationId = applicationId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !applicationId.isEmpty else { return }
        await perform(
            .approveCommunityApplication(idOrSlug: slug, applicationId: applicationId),
            success: UiMessage(.nativeSwiftCommunityStatusApprovedApplication)
        )
    }

    func rejectApplication(applicationId: String, reason: String) async {
        let applicationId = applicationId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !applicationId.isEmpty else { return }
        await perform(
            .rejectCommunityApplication(
                idOrSlug: slug,
                applicationId: applicationId,
                rejectionReason: reason.trimmedOrNil ?? ""
            ),
            success: UiMessage(.nativeSwiftCommunityStatusRejectedApplication)
        )
    }

    func revokeInvite(inviteId: String) async {
        let inviteId = inviteId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !inviteId.isEmpty else { return }
        await perform(
            .revokeCommunityInvite(idOrSlug: slug, inviteId: inviteId),
            success: UiMessage(.nativeSwiftCommunityStatusRevokedInvite)
        )
    }

    func updatePinnedPosts(postIds: [String]) async {
        let postIds = postIds.map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }.filter { !$0.isEmpty }
        await perform(
            .updateCommunityPinnedPosts(idOrSlug: slug, postIds: postIds),
            success: UiMessage(.nativeSwiftCommunityStatusUpdatedPinnedPosts)
        )
    }

    func setModeratorVacation(endsAt: Date?) async {
        await perform(
            .setCommunityModeratorVacation(idOrSlug: slug, endsAt: endsAt),
            success: UiMessage(.nativeSwiftCommunityStatusUpdatedVacation)
        )
    }

    func clearModeratorVacation() async {
        await perform(
            .clearCommunityModeratorVacation(idOrSlug: slug),
            success: UiMessage(.nativeSwiftCommunityStatusClearedVacation)
        )
    }

    func setSuppressCommunityDigestsWhileOnVacation(_ suppress: Bool) async {
        await perform(
            .setSuppressCommunityDigestsWhileOnVacation(idOrSlug: slug, suppress: suppress),
            success: UiMessage(
                suppress
                    ? .nativeSwiftCommunityStatusCommunityDigestsPaused
                    : .nativeSwiftCommunityStatusCommunityDigestsResumed
            )
        )
    }
}
