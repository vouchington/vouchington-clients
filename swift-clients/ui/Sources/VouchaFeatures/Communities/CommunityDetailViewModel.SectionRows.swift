import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadSettingsRows() -> [NativeRouteDestinationRow] {
        guard let detail = communityDetail else { return [] }
        let community = detail.community
        return settingsAccessRows(community)
            + settingsContentRows(community)
            + settingsMetadataRows(community)
    }

    private func settingsAccessRows(_ community: Community) -> [NativeRouteDestinationRow] {
        [
            .init(
                icon: "gearshape",
                title: .message(.nativeSwiftCommunityRowsVisibility),
                detail: .message(community.visibility.titleKey)
            ),
            .init(
                icon: "person.3",
                title: .message(.nativeSwiftCommunityRowsRoster),
                detail: .message(community.memberRosterVisibility.titleKey)
            ),
            .init(
                icon: "checkmark.shield",
                title: .message(.nativeSwiftCommunityRowsPostApproval),
                detail: .message(
                    community.postApprovalRequiredAt == nil
                        ? .nativeSwiftCommunityRowsOpen
                        : .nativeSwiftCommunityRowsRequired
                )
            ),
            .init(
                icon: "envelope",
                title: .message(.nativeSwiftCommunityRowsInvites),
                detail: .message(
                    community.memberInvitesAllowedAt == nil
                        ? .nativeSwiftCommunityRowsDisabled
                        : .nativeSwiftCommunityRowsEnabled
                )
            )
        ]
    }

    private func settingsContentRows(_ community: Community) -> [NativeRouteDestinationRow] {
        [
            .init(
                icon: "doc.text",
                title: .message(.nativeSwiftCommunityRowsReviewPosts),
                detail: .message(
                    community.shouldAllowReviewPosts
                        ? .nativeSwiftCommunityRowsEnabled
                        : .nativeSwiftCommunityRowsDisabled
                )
            ),
            .init(
                icon: "chart.bar",
                title: .message(.nativeSwiftCommunityRowsDataPointPosts),
                detail: .message(
                    community.shouldAllowDataPointPosts
                        ? .nativeSwiftCommunityRowsEnabled
                        : .nativeSwiftCommunityRowsDisabled
                )
            ),
            .init(
                icon: "bookmark",
                title: .message(.nativeSwiftCommunityRowsListType),
                detail: community.listType.map { .message($0.titleKey) }
                    ?? .message(.nativeSwiftCommunityRowsNone)
            )
        ]
    }

    private func settingsMetadataRows(_ community: Community) -> [NativeRouteDestinationRow] {
        [
            .init(
                icon: "globe",
                title: .message(.nativeSwiftCommunityRowsLanguage),
                detail: community.defaultLanguage.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftCommunityRowsDefaultValue)
            ),
            .init(
                icon: "text.alignleft",
                title: .message(.nativeSwiftCommunityRowsRules),
                detail: community.rulesMarkdown.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftCommunityRowsNone)
            )
        ]
    }

    func loadModmailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let modmailThreadId {
            let response: CommunityModmailMessagesResponse = try await client.send(
                .communityModmailMessages(idOrSlug: slug, conversationId: modmailThreadId, limit: 25)
            )
            modmailEndCursor = response.pageInfo.endCursor
            modmailRowIds = Set(response.results.map(\.id))
            return response.results.map { message in
                .init(
                    icon: "bubble.left.and.text.bubble.right",
                    title: message.senderUsername.map { .verbatim("@\($0)") }
                        ?? .message(.nativeSwiftCommunitiesMessage),
                    detail: .verbatim(message.bodyText)
                )
            }
        }
        modmailThreadPagination.reset()
        guard let request = modmailThreadPagination.beginNextPage() else { return [] }
        let response: CommunityModmailThreadsResponse = try await client.send(
            .communityModmail(idOrSlug: slug, after: request.cursor, limit: 25)
        )
        modmailThreadPagination.complete(
            request,
            items: response.results,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
        modmailRowIds = Set(response.results.map(\.id))
        return response.results.map { result in
            .init(
                icon: "bubble.left.and.bubble.right",
                title: .message(.nativeSwiftCommunityRowsThread, parameters: ["id": result.id]),
                detail: .message(.nativeSwiftCommunityRowsOpen)
            )
        }
    }

    func loadListItemCounts(client: APIClient) async throws -> CommunityListItemCounts {
        if let listItemCounts {
            return listItemCounts
        }
        let response: CommunityListItemCountsResponse = try await client
            .send(.communityListItemCounts(idOrSlug: slug))
        return response.counts
    }

}
