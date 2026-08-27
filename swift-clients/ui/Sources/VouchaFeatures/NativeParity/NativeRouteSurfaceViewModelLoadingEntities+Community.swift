import VouchaAPI
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadCommunityDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let detail: NativeCommunityDetailResponse = try await client.send(
            .community(idOrSlug: routeEntityId)
        )
        if detail.hasPendingApplication == true, detail.membership == nil {
            return communityDetailRows(
                detail: detail,
                members: NativeCommunityMembersResponse(results: [], communityMembers: [:], users: [:]),
                posts: NativeCommunityPostsResponse(results: [], posts: [:]),
                counts: NativeCommunityListItemCounts(topic: 0, rssFeed: 0, post: 0, urlHostname: 0, url: 0)
            )
        }

        async let members: NativeCommunityMembersResponse = client.send(
            .communityMembers(idOrSlug: routeEntityId, limit: 20)
        )
        async let posts: NativeCommunityPostsResponse = client.send(
            .communityPosts(idOrSlug: routeEntityId, limit: 20)
        )
        async let counts: NativeCommunityListItemCounts = client.send(
            .communityListItemCounts(idOrSlug: routeEntityId)
        )

        return try await communityDetailRows(
            detail: detail,
            members: members,
            posts: posts,
            counts: counts
        )
    }

    private func communityDetailRows(
        detail: NativeCommunityDetailResponse,
        members: NativeCommunityMembersResponse,
        posts: NativeCommunityPostsResponse,
        counts: NativeCommunityListItemCounts
    ) -> [NativeRouteDestinationRow] {
        var rows = [
            row(
                "person.3",
                .verbatim(detail.community.displayTitle(fallback: routeEntityId)),
                .verbatim(detail.community.displayDetail)
            )
        ]
        if let metrics = detail.communityMetrics {
            rows.append(row(
                "chart.bar",
                appText(.nativeSwiftNavigationTitlesAnalytics),
                communityActivityDetail(
                    metrics: metrics,
                    members: members,
                    posts: posts,
                    counts: counts
                )
            ))
        }
        if detail.hasPendingApplication == true {
            rows.append(row(
                "clock",
                appText(.nativeSwiftCommunityRowsApplicationPending),
                appText(.nativeSwiftCommunityRowsApplicationPendingDetail)
            ))
        }
        rows.append(row(
            "person.2",
            appText(.nativeSwiftCommunitiesMembers),
            countText(members.results.count, item: "member")
        ))
        rows.append(row(
            "doc.text",
            appText(.nativeSwiftNavigationTitlesPosts),
            countText(posts.results.count, item: "post")
        ))
        rows.append(row(
            "list.bullet",
            appText(.nativeSwiftNavigationTitlesLists),
            communityListItemsDetail(counts: counts)
        ))
        rows.append(contentsOf: communityMembersRows(members))
        rows.append(contentsOf: communityPostsRows(posts))
        return rows
    }

    private func communityActivityDetail(
        metrics: NativeCommunityMetrics,
        members: NativeCommunityMembersResponse,
        posts _: NativeCommunityPostsResponse,
        counts _: NativeCommunityListItemCounts
    ) -> UiVerbatimText {
        .count(metrics.memberCount ?? members.results.count, item: "member")
    }

    private func communityListItemsDetail(counts: NativeCommunityListItemCounts) -> UiVerbatimText {
        .count(counts.total, item: "listItem")
    }

    private func communityMembersRows(
        _ members: NativeCommunityMembersResponse
    ) -> [NativeRouteDestinationRow] {
        members.results.prefix(3).compactMap { result in
            guard let member = members.communityMembers[result.id] else { return nil }
            let user = members.users[member.userId]
            return row(
                "person",
                .verbatim(user?.displayTitle(fallback: member.userId) ?? member.userId),
                communityMemberRoleText(member.role)
            )
        }
    }

    private func communityPostsRows(
        _ posts: NativeCommunityPostsResponse
    ) -> [NativeRouteDestinationRow] {
        posts.results.prefix(3).compactMap { result in
            guard let post = posts.posts[result.id] else { return nil }
            return row(
                "doc.text",
                .verbatim(post.title ?? post.slug ?? post.id),
                postTypeText(post.postType)
            )
        }
    }
}
