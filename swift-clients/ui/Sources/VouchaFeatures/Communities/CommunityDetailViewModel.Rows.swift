import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        try await loadRows(
            client: client,
            after: nil,
            tab: selectedTab,
            revision: communityLoadRevision
        ).items.map(\.row)
    }

    func loadRows(
        client: APIClient,
        after: String?,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws -> CommunityForwardPage {
        if tab.listItemType != nil {
            return try await loadListItemRows(client: client, after: after, tab: tab)
        }
        if tab.isManagementTab {
            return try await loadManagementRows(client: client, after: after, tab: tab, revision: revision)
        }
        if tab.isParticipationTab {
            return try await loadParticipationRows(client: client, after: after, tab: tab)
        }
        return try await loadContentRows(client: client, after: after, tab: tab)
    }

    private func loadContentRows(
        client: APIClient,
        after: String?,
        tab: CommunitySurfaceTab
    ) async throws -> CommunityForwardPage {
        switch tab {
        case .posts:
            try await loadPostRows(client: client, after: after)
        case .news:
            try await loadNewsRows(client: client, after: after)
        case .members:
            try await loadMemberRows(client: client, after: after)
        case .lists:
            try await .terminal(loadListRows(client: client))
        default:
            .terminal([])
        }
    }

    private func loadParticipationRows(
        client: APIClient,
        after: String?,
        tab: CommunitySurfaceTab
    ) async throws -> CommunityForwardPage {
        switch tab {
        case .pinnedPosts:
            return try await .terminal(loadPinnedPostRows(client: client))
        case .applications:
            return try await loadApplicationRows(client: client, after: after)
        case .invites:
            if shouldSkipInviteRows {
                return .terminal([])
            }
            return try await loadInviteRows(client: client, after: after)
        default:
            return .terminal([])
        }
    }

    private func loadPostRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: NativeCommunityPostsResponse = try await client.send(
            .communityPosts(
                idOrSlug: slug,
                after: after,
                limit: 10
            )
        )
        postEmbedsByPostId.merge(response.postLinkEmbeds ?? [:]) { _, new in new }
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let post = response.posts[result.id] else { return nil }
            return .init(
                id: result.id,
                row: .init(
                    id: result.id,
                    icon: "doc.text",
                    title: .verbatim(post.title ?? post.slug ?? post.id),
                    detail: postTypeText(post.postType)
                )
            )
        }
        return .init(
            items: items,
            endCursor: response.pageInfo?.endCursor,
            hasMore: response.pageInfo?.hasNextPage ?? false
        )
    }

    private func loadMemberRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityMembersResponse = try await client.send(
            .communityMembers(idOrSlug: slug, after: after, limit: 10)
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let member = response.communityMembers[result.id] else { return nil }
            return .init(
                id: result.id,
                row: .init(
                    icon: "person",
                    title: .verbatim(response.users[member.userId]?.username ?? member.userId),
                    detail: .message(member.role.titleKey)
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }

    private func loadListRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let counts = try await loadListItemCounts(client: client)
        return [
            .init(
                icon: "tag",
                title: .message(.nativeSwiftRouteSurfaceTopics),
                detail: .count(counts.topic, item: "item")
            ),
            .init(
                icon: "dot.radiowaves.left.and.right",
                title: .message(.navSources),
                detail: .count(counts.rssFeed, item: "item")
            ),
            .init(
                icon: "doc.text",
                title: .message(.nativeSwiftRouteSurfacePosts),
                detail: .count(counts.post, item: "item")
            ),
            .init(
                icon: "link",
                title: .message(.nativeSwiftCommunitiesDomainsAndUrls),
                detail: .count(counts.urlHostname + counts.url, item: "item")
            )
        ]
    }

    private func loadListItemRows(
        client: APIClient,
        after: String?,
        tab: CommunitySurfaceTab
    ) async throws -> CommunityForwardPage {
        guard let itemType = tab.listItemType else { return .terminal([]) }
        let response: CommunityListItemsResponse = try await client.send(
            .communityListItems(
                idOrSlug: slug,
                itemType: itemType,
                after: after,
                limit: 10
            )
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let item = response.communityListItems[result.id] else { return nil }
            return .init(
                id: result.id,
                row: .init(
                    icon: listItemIcon(for: itemType),
                    title: .verbatim(item.entityId),
                    detail: .message(
                        .nativeSwiftCommunitiesOrderPosition,
                        numberParameters: ["position": Double(item.orderIndex + 1)]
                    )
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }

}
