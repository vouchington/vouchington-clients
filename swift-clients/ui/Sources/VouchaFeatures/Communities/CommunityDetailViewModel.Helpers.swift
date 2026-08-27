import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func join() async {
        await perform(
            .joinCommunity(idOrSlug: slug),
            success: UiMessage(.nativeSwiftCommunityStatusJoinedCommunity)
        )
    }

    func leave() async {
        await perform(
            .leaveCommunity(idOrSlug: slug),
            success: UiMessage(.nativeSwiftCommunityStatusLeftCommunity)
        )
    }

    func archive() async {
        await perform(
            .archiveCommunity(idOrSlug: slug),
            success: UiMessage(.nativeSwiftCommunityStatusArchivedCommunity)
        )
    }

    func unarchive() async {
        await perform(
            .unarchiveCommunity(idOrSlug: slug),
            success: UiMessage(.nativeSwiftCommunityStatusUnarchivedCommunity)
        )
    }

    func apply() async {
        guard hasRequiredApplicationAnswers else {
            state = .error(UiMessage(.nativeSwiftCommunityStatusApplicationAnswersMissing))
            return
        }
        let success = await perform(
            .submitCommunityApplication(
                idOrSlug: slug,
                answers: applicationAnswerPayload,
                message: applicationMessage.trimmedOrNil
            ),
            success: UiMessage(.nativeSwiftCommunityStatusApplicationSubmitted)
        )
        if success {
            applicationMessage = ""
            applicationAnswers = [:]
        }
    }

    func invite() async {
        let recipient = inviteRecipient.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !recipient.isEmpty else { return }
        let endpoint = recipient.contains("@")
            ? Endpoint.sendCommunityInvite(idOrSlug: slug, email: recipient)
            : Endpoint.sendCommunityInvite(idOrSlug: slug, username: recipient)
        let success = await perform(
            endpoint,
            success: UiMessage(.nativeSwiftCommunityStatusInviteSent)
        )
        if success {
            inviteRecipient = ""
        }
    }

    @discardableResult
    func perform(_ endpoint: Endpoint, success: UiMessage) async -> Bool {
        guard let client else { return false }
        state = .loading
        do {
            let _: EmptyResponse = try await client.send(endpoint)
            statusMessage = success
            await load()
            return true
        } catch {
            state = .error(UiMessage(.nativeSwiftCommunityStatusActionFailed))
            return false
        }
    }

    func activityText(_ metrics: CommunityMetrics?) -> UiVerbatimText {
        let memberCount = metrics?.memberCount ?? 0
        let postCount = metrics?.postCount ?? 0
        let listItemCount = metrics?.listItemCount ?? 0
        return .message(
            .nativeSwiftCommunitiesActivitySummary,
            numberParameters: [
                "members": Double(memberCount),
                "posts": Double(postCount),
                "items": Double(listItemCount)
            ]
        )
    }

    var hasRequiredApplicationAnswers: Bool {
        let payload = applicationAnswerPayload
        return applicationQuestions.allSatisfy { question in
            !question.required || payload[question.id] != nil
        }
    }

    var applicationAnswerPayload: [String: DecodedJSONValue] {
        guard !applicationQuestions.isEmpty else { return applicationAnswers }
        return applicationQuestions.reduce(into: [:]) { payload, question in
            guard let value = applicationAnswers[question.id]?.normalized(for: question.fieldType) else { return }
            payload[question.id] = value
        }
    }

    func loadApplicationQuestionsIfNeeded(
        client: APIClient,
        tab: CommunitySurfaceTab? = nil
    ) async throws -> [CommunityApplicationQuestion] {
        guard isApplicationFormRoute || (tab ?? selectedTab) == .applications else { return [] }
        let response: CommunityApplicationQuestionsResponse = try await client
            .send(.communityApplicationQuestions(idOrSlug: slug))
        return response.questions
    }

    func loadVisibleListItemCounts(
        client: APIClient,
        detail: CommunityResponse
    ) async throws -> CommunityListItemCounts {
        guard canLoadRows(community: detail.community, membership: detail.membership) else {
            return CommunityListItemCounts(topic: 0, rssFeed: 0, post: 0, urlHostname: 0, url: 0)
        }
        let response: CommunityListItemCountsResponse = try await client.send(
            .communityListItemCounts(idOrSlug: slug)
        )
        return response.counts
    }

    func canLoadRows(community: Community, membership: CommunityMember?) -> Bool {
        community.visibility == .public || membership != nil || isAdministrator
    }

    func canLoadSelectedRows(community: Community, membership: CommunityMember?) -> Bool {
        if canLoadRows(community: community, membership: membership) {
            return true
        }
        switch selectedTab {
        case .moderation, .modlog, .moderationAnalytics:
            return isSiteModerator
        case .modmail:
            return isSiteModerator || membership != nil
        default:
            return false
        }
    }
}
