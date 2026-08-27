import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadModlogRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityModlogResponse = try await client.send(
            .communityModlog(idOrSlug: slug, after: after)
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let entry = response.moderatorActions[result.id] else { return nil }
            let actor = entry.actorId.flatMap { response.users[$0]?.username } ?? entry.actorId
            let target = entry.targetUserId.flatMap { response.users[$0]?.username } ?? entry.targetUserId
            return .init(
                id: result.id,
                row: .init(
                    icon: "list.bullet.rectangle",
                    title: .verbatim(entry.actionType),
                    detail: actor.map { .verbatim(joinedDetailParts([$0, target, entry.reason])) }
                        ?? .message(.nativeSwiftCommunityRowsSystem)
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }

    func loadBansRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityBansResponse = try await client.send(
            .communityBans(idOrSlug: slug, after: after, limit: 25)
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let ban = response.communityBans[result.id] else { return nil }
            let user = response.users[ban.userId]?.username ?? ban.userId
            let detail: UiVerbatimText = if let expiresAt = ban.expiresAt {
                .message(
                    ban.reason == nil
                        ? .nativeSwiftCommunityRowsExpires
                        : .nativeSwiftCommunityRowsExpiresWithReason,
                    parameters: ban.reason.map { ["reason": $0] } ?? [:],
                    dateParameters: ["date": .init(expiresAt, dateStyle: .numeric)]
                )
            } else {
                .message(
                    ban.reason == nil
                        ? .nativeSwiftCommunityRowsNoExpiry
                        : .nativeSwiftCommunityRowsNoExpiryWithReason,
                    parameters: ban.reason.map { ["reason": $0] } ?? [:]
                )
            }
            return .init(
                id: result.id,
                row: .init(icon: "nosign", title: .verbatim(user), detail: detail)
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }

    func loadRestrictionsRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: CommunityRestrictionsResponse = try await client.send(
            .communityRestrictions(idOrSlug: slug, after: after)
        )
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let restriction = response.communityRestrictions[result.id] else { return nil }
            let detail = UiVerbatimText.message(
                restriction.liftedAt == nil
                    ? .nativeSwiftCommunityRowsActiveWithReason
                    : .nativeSwiftCommunityRowsLiftedWithReason,
                parameters: ["reason": restriction.reason ?? ""]
            )
            return .init(
                id: result.id,
                row: .init(
                    icon: "lock.shield",
                    title: .message(restriction.restrictionType.titleKey),
                    detail: detail
                )
            )
        }
        return .init(items: items, endCursor: response.pageInfo.endCursor, hasMore: response.pageInfo.hasNextPage)
    }
}
