import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadPinnedPostRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: CommunityPinnedPostsResponse = try await client.send(
            .communityPinnedPosts(idOrSlug: slug)
        )
        return response.pinnedPosts.map { pinnedPost in
            .init(
                icon: "pin",
                title: .message(
                    .nativeSwiftCommunitiesPinnedPostValue,
                    textParameters: ["id": .verbatim(pinnedPost.postId)]
                ),
                detail: .message(
                    .nativeSwiftCommunitiesOrderPosition,
                    numberParameters: ["position": Double(pinnedPost.orderIndex + 1)]
                )
            )
        }
    }

    func loadApplicationRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityApplicationsResponse = try await client.send(
            .communityApplications(idOrSlug: slug, after: after, limit: 10)
        )
        let items = response.results.map { result in
            CommunityForwardRow(
                id: result.id,
                row: .init(
                    icon: "doc.badge.clock",
                    title: .message(
                        .nativeSwiftCommunitiesApplicationValue,
                        textParameters: ["id": .verbatim(result.id)]
                    ),
                    detail: .message(.nativeSwiftCommunitiesAwaitingReview)
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }

    func loadInviteRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityInvitesResponse = try await client.send(
            .communityInvites(idOrSlug: slug, after: after, limit: 10)
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let invite = response.communityInvites[result.id] else { return nil }
            return .init(
                id: result.id,
                row: .init(
                    icon: "envelope",
                    title: .verbatim(invite.invitedEmail ?? invite.invitedUserId ?? invite.code),
                    detail: .message(
                        invite.acceptedAt == nil
                            ? .nativeSwiftCommunityRowsOpen
                            : .nativeSwiftCommunitiesAccepted
                    )
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }
}
