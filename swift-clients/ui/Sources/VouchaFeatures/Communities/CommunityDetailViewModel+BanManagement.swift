import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadBanHistory(more: Bool = false) async {
        await loadBanSummaries(userId: nil, more: more)
    }

    func loadActiveBans(userId: String, more: Bool = false) async {
        guard !userId.isEmpty else { return }
        await loadBanSummaries(userId: userId, more: more)
    }

    private func loadBanSummaries(userId: String?, more: Bool) async {
        guard let client, !isLoadingBans, !isLiftingBans else { return }
        let isActive = userId != nil
        let sameUser = userId == activeBansUserId
        let cursor = isActive ? activeBansCursor : banHistoryCursor
        guard !more || (cursor != nil && (!isActive || sameUser)) else { return }
        let revision = banRequestRevision
        isLoadingBans = true
        defer { isLoadingBans = false }
        banError = nil
        do {
            let endpoint: Endpoint = if let userId {
                .activeCommunityBans(idOrSlug: slug, userId: userId, after: more ? cursor : nil)
            } else {
                .communityBans(idOrSlug: slug, after: more ? cursor : nil, limit: 25)
            }
            let response: CommunityBansResponse = try await client.send(endpoint)
            guard revision == banRequestRevision else { return }
            let page = response.results.compactMap { response.communityBans[$0.id] }
            var rows = more ? (isActive ? activeBans : banHistory) : []
            var ids = Set(rows.map(\.id))
            rows.append(contentsOf: page.filter { ids.insert($0.id).inserted })
            let next = response.pageInfo.hasNextPage ? response.pageInfo.endCursor : nil
            if isActive {
                activeBansUserId = userId ?? ""
                activeBans = rows
                activeBansCursor = next
            } else {
                banHistory = rows
                banHistoryCursor = next
            }
        } catch {
            banError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)
        }
    }

    func loadBanDetail(banId: String) async {
        guard let client else { return }
        banDetailRevision += 1
        let revision = banDetailRevision
        banDetail = nil
        banError = nil
        do {
            let response: CommunityBanResponse = try await client.send(.communityBan(idOrSlug: slug, banId: banId))
            guard revision == banDetailRevision else { return }
            banDetail = response.communityBan
        } catch {
            banError = UiMessage(UiMessageKey.moderationCommunityBansDetailLoadFailed)
        }
    }

    func requestLiftAllBans(userId: String) {
        guard !userId.isEmpty else { return }
        pendingLiftAllUserId = userId
    }

    func confirmLiftAllBans() async {
        guard let userId = pendingLiftAllUserId else { return }
        pendingLiftAllUserId = nil
        await liftBans(.liftAllCommunityBans(idOrSlug: slug, userId: userId))
    }

    func liftBan(banId: String) async {
        guard !banId.isEmpty else { return }
        await liftBans(.liftCommunityBan(idOrSlug: slug, banId: banId))
    }

    private func liftBans(_ endpoint: Endpoint) async {
        guard let client, !isLiftingBans else { return }
        isLiftingBans = true
        defer { isLiftingBans = false }
        banError = nil
        do {
            let response: CommunityBanLiftResponse = try await client.send(endpoint)
            banRequestRevision += 1
            banDetailRevision += 1
            let lifted = Set(response.liftedBanIds)
            activeBans.removeAll { lifted.contains($0.id) }
            banHistory.removeAll { lifted.contains($0.id) }
            if selectedTab == .bans {
                communityLoadRevision += 1
                rowPagination.invalidateRequestsPreservingPage()
                rowPagination.remove { lifted.contains($0.id) }
                summary.rows = rowPagination.items.map(\.row)
                state = .loaded
            }
            if let detail = banDetail, lifted.contains(detail.id) { banDetail = nil }
            statusMessage = UiMessage(.nativeSwiftCommunityStatusLiftedBan)
        } catch let failure as CommunityBanLiftFailure {
            if failure.statusCode == 403 {
                banError = UiMessage(UiMessageKey.moderationCommunityBansLiftAllForbidden)
            } else if let cap = failure.cap, cap > 0 {
                banError = UiMessage(
                    UiMessageKey.moderationCommunityBansLiftAllLimit,
                    numberParameters: ["cap": Double(cap)]
                )
            } else {
                banError = UiMessage(UiMessageKey.moderationCommunityBansLiftAllLimitUnknown)
            }
        } catch {
            banError = UiMessage(UiMessageKey.moderationCommunityBansLiftFailed)
        }
    }
}
