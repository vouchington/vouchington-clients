import VouchaModels

extension CommunityDetailViewModel {
    var visibleTabs: [CommunitySurfaceTab] {
        if isApplicationFormRoute {
            return []
        }
        if let detail = communityDetail,
           !canLoadRows(community: detail.community, membership: detail.membership),
           !canViewStaffModeration(membership: detail.membership),
           !canViewModmail(membership: detail.membership) {
            return []
        }
        return summary.tabs.isEmpty ? [.posts] : summary.tabs
    }

    func visibleTabs(
        for community: Community,
        membership: CommunityMember?,
        counts: CommunityListItemCounts
    ) -> [CommunitySurfaceTab] {
        var tabs: [CommunitySurfaceTab] = []

        if canLoadRows(community: community, membership: membership) {
            tabs.append(.posts)
        }

        appendContentTabs(to: &tabs, community: community, membership: membership, counts: counts)
        appendManagementTabs(
            to: &tabs,
            community: community,
            membership: membership
        )

        return tabs
    }

    private func appendContentTabs(
        to tabs: inout [CommunitySurfaceTab],
        community: Community,
        membership: CommunityMember?,
        counts: CommunityListItemCounts
    ) {
        if counts.topic > 0 || counts.rssFeed > 0 {
            tabs.append(.news)
        }
        if canLoadRows(community: community, membership: membership) {
            tabs.append(.members)
        }
        if canLoadRows(community: community, membership: membership) {
            tabs.append(.lists)
            if counts.topic > 0 {
                tabs.append(.listTopics)
            }
            if counts.rssFeed > 0 {
                tabs.append(.listSources)
            }
            if counts.post > 0 {
                tabs.append(.listPosts)
            }
            if counts.urlHostname > 0 {
                tabs.append(.listDomains)
            }
            if counts.url > 0 {
                tabs.append(.listUrls)
            }
        }
    }

    private func appendManagementTabs(
        to tabs: inout [CommunitySurfaceTab],
        community: Community,
        membership: CommunityMember?
    ) {
        if canViewPinnedPosts(community: community, membership: membership) {
            tabs.append(.pinnedPosts)
        }
        if canViewStaffModeration(membership: membership) {
            tabs.append(contentsOf: [
                .moderation,
                .modlog
            ])
        }
        if canViewModmail(membership: membership) {
            tabs.append(.modmail)
        }
        if canModerate(membership: membership) {
            tabs.append(contentsOf: [
                .bans,
                .restrictions
            ])
        }
        if canManageModeratorVacation(membership: membership) {
            tabs.append(.moderatorVacation)
        }
        if membership != nil || canViewStaffModeration(membership: membership) {
            tabs.append(.moderationAnalytics)
        }
        if canReviewApplications(membership: membership) {
            tabs.append(.applications)
        }
        if canInvite(community: community, membership: membership) {
            tabs.append(.invites)
        }
        if membership?.role == .owner {
            tabs.append(.settings)
        }
    }

    private func canModerate(membership: CommunityMember?) -> Bool {
        isAdministrator || membership?.role == .owner || membership?.role == .moderator
    }

    var canModerateCommunity: Bool {
        canModerate(membership: communityDetail?.membership)
    }

    var canViewRawModerationAnalytics: Bool {
        canViewStaffModeration(membership: communityDetail?.membership)
    }

    var canManageModeratorVacation: Bool {
        canManageModeratorVacation(membership: communityDetail?.membership)
    }

    private func canManageModeratorVacation(membership: CommunityMember?) -> Bool {
        membership?.role == .owner || membership?.role == .moderator
    }

    private func canViewStaffModeration(membership: CommunityMember?) -> Bool {
        canModerate(membership: membership) || isSiteModerator
    }

    private func canReviewApplications(membership: CommunityMember?) -> Bool {
        canModerate(membership: membership)
    }

    private func canViewPinnedPosts(community: Community, membership: CommunityMember?) -> Bool {
        canLoadRows(community: community, membership: membership)
    }

    private func canViewModmail(membership: CommunityMember?) -> Bool {
        isAdministrator || isSiteModerator || membership != nil
    }

    private func canInvite(community: Community, membership: CommunityMember?) -> Bool {
        guard let membership else { return false }
        return canModerate(membership: membership) || community.memberInvitesAllowedAt != nil
    }
}
