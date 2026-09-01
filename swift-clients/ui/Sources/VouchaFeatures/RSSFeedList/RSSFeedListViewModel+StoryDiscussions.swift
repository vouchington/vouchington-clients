import VouchaCore
import VouchaModels
import VouchaAPI

struct StoryDiscussionDestination: Identifiable, Hashable {
    let postId: String
    let postType: PostType

    var id: String {
        "\(postType.rawValue)|\(postId)"
    }

    var routeMatch: NativeRouteMatch {
        NativeRouteMatch(
            path: "/\(postType.rawValue)/\(postId)",
            template: "/\(postType.rawValue)/:id",
            params: ["id": postId]
        )
    }
}

extension RSSFeedListViewModel {
    func canStartStoryDiscussion(rssFeedItemId: String) -> Bool {
        guard contentType == .news else { return false }
        guard let storyId = storyIdsByItemId[rssFeedItemId] else { return false }
        guard storyPostIdsByStoryId[storyId] == nil else { return false }
        guard fallbackStoryDestinationsByStoryId[storyId] == nil else { return false }
        guard !startedDiscussionStoryIds.contains(storyId) else { return false }
        let memberIds = storyMemberIdsByStoryId[storyId] ?? []
        return memberIds.contains { $0 != rssFeedItemId }
    }

    func isStartingStoryDiscussion(rssFeedItemId: String) -> Bool {
        guard let storyId = storyIdsByItemId[rssFeedItemId] else { return false }
        return startingDiscussionStoryIds.contains(storyId)
    }

    func storyDiscussionDestination(rssFeedItemId: String) -> StoryDiscussionDestination? {
        guard contentType == .news else { return nil }
        guard let storyId = storyIdsByItemId[rssFeedItemId] else { return nil }
        if let destination = storyDiscussionDestinationsByStoryId[storyId] {
            return destination
        }
        if let destination = fallbackStoryDestinationsByStoryId[storyId] {
            return destination
        }
        guard let postId = storyPostIdsByStoryId[storyId] else { return nil }
        return StoryDiscussionDestination(postId: postId, postType: .story)
    }

    func startStoryDiscussion(rssFeedItemId: String) async -> StoryDiscussionDestination? {
        guard canStartStoryDiscussion(rssFeedItemId: rssFeedItemId),
              let storyId = storyIdsByItemId[rssFeedItemId],
              !startingDiscussionStoryIds.contains(storyId)
        else { return nil }
        startingDiscussionStoryIds.insert(storyId)
        defer { startingDiscussionStoryIds.remove(storyId) }

        do {
            let storyIntent = "story\u{001F}\(storyId)"
            let idempotencyKey = await contributionIdentity.key(surface: "story-discussion", canonicalIntent: storyIntent)
            let result: StoryPostResult = try await emailVerificationGate.perform(rollbackOnFailure: {}, {
                try await client.send(.createStoryDiscussion(storyId: storyId, idempotencyKey: idempotencyKey))
            })
            await contributionIdentity.complete(surface: "story-discussion", canonicalIntent: storyIntent)
            storyPostIdsByStoryId[storyId] = result.post.id
            let destination = StoryDiscussionDestination(postId: result.post.id, postType: result.post.postType)
            storyDiscussionDestinationsByStoryId[storyId] = destination
            startedDiscussionStoryIds.insert(storyId)
            state = .loaded
            return destination
        } catch let error as ContributionAdmissionFailure {
            state = .error(.api(statusCode: error.statusCode, preconditionCode: error.code))
            return nil
        } catch let error as VouchaError where error.isFeedNotDiscoverable {
            return await startLinkDiscussionFallback(rssFeedItemId: rssFeedItemId, storyId: storyId)
        } catch let error as VouchaError where error.isConflict {
            await reload()
            return storyDiscussionDestination(rssFeedItemId: rssFeedItemId)
        } catch let error as VouchaError {
            state = .error(error)
            return nil
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            return nil
        }
    }

    private func startLinkDiscussionFallback(
        rssFeedItemId: String,
        storyId: String
    ) async -> StoryDiscussionDestination? {
        do {
            let rssIntent = "rss\u{001F}\(rssFeedItemId)"
            let idempotencyKey = await contributionIdentity.key(surface: "rss-discussion", canonicalIntent: rssIntent)
            let result: PostEnvelope = try await emailVerificationGate.perform(rollbackOnFailure: {}, {
                try await client.send(.createRssFeedItemDiscussion(
                    rssFeedItemId: rssFeedItemId, idempotencyKey: idempotencyKey
                ))
            })
            await contributionIdentity.complete(surface: "rss-discussion", canonicalIntent: rssIntent)
            storyPostIdsByStoryId[storyId] = result.post.id
            let destination = StoryDiscussionDestination(postId: result.post.id, postType: result.post.postType)
            storyDiscussionDestinationsByStoryId[storyId] = destination
            fallbackStoryDestinationsByStoryId[storyId] = destination
            startedDiscussionStoryIds.insert(storyId)
            state = .loaded
            return destination
        } catch let error as ContributionAdmissionFailure {
            state = .error(.api(statusCode: error.statusCode, preconditionCode: error.code))
            return nil
        } catch let error as VouchaError {
            state = .error(error)
            return nil
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            return nil
        }
    }
}

private extension VouchaError {
    var isConflict: Bool {
        switch self {
        case let .api(statusCode, _), let .apiMessage(statusCode, _, _):
            statusCode == 409
        default:
            false
        }
    }

    var isFeedNotDiscoverable: Bool {
        switch self {
        case let .forbidden(preconditionCode),
             let .api(_, preconditionCode),
             let .apiMessage(_, preconditionCode, _):
            preconditionCode == "FEED_NOT_DISCOVERABLE"
        default:
            false
        }
    }
}
