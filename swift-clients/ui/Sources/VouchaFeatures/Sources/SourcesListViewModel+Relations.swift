import VouchaAPI
import VouchaLocalization
import VouchaModels

private struct BookmarkToggleHandlers {
    let isInFlight: (String) -> Bool
    let begin: (String) -> Void
    let finish: (String) -> Void
    let isActive: (String) -> Bool
    let activate: (String) -> Void
    let deactivate: (String) -> Void
}

@MainActor
public extension SourcesListViewModel {
    func toggleFollow(sourceId: String) async {
        guard userId != nil, !togglingSourceIds.contains(sourceId) else { return }
        togglingSourceIds.insert(sourceId)
        defer { togglingSourceIds.remove(sourceId) }

        let wasFollowing = followedSourceIds.contains(sourceId)
        let trackedSource = source(with: sourceId)
        let rollbackPosition = wasFollowing ? rollbackPosition(for: sourceId) : nil
        setSourceFollow(
            sourceId: sourceId,
            source: trackedSource,
            following: !wasFollowing,
            rollbackPosition: rollbackPosition
        )
        do {
            let endpoint = wasFollowing
                ? Endpoint.unfollowRssFeed(rssFeedId: sourceId)
                : Endpoint.followRssFeed(rssFeedId: sourceId)
            let _: EmptyResponse = try await client.send(endpoint)
            statusMessage = .message(
                wasFollowing ? .nativeSwiftSourcesSourceUnfollowed : .nativeSwiftSourcesSourceFollowed
            )
        } catch {
            setSourceFollow(
                sourceId: sourceId,
                source: trackedSource,
                following: wasFollowing,
                rollbackPosition: rollbackPosition
            )
            statusMessage = .message(
                wasFollowing ? .nativeSwiftSourcesUnableToUnfollowSource : .nativeSwiftSourcesUnableToFollowSource
            )
        }
    }

    func toggleMute(sourceId: String) async {
        guard !togglingSourceIds.contains(sourceId) else { return }
        let wasFollowing = followedSourceIds.contains(sourceId)
        let trackedSource = source(with: sourceId)
        await toggleBookmark(
            id: sourceId,
            predicate: "mute",
            entityType: "rss_feed",
            handlers: .init(
                isInFlight: { self.togglingMutedSourceIds.contains($0) || self.togglingSourceIds.contains($0) },
                begin: { self.togglingMutedSourceIds.insert($0) },
                finish: { self.togglingMutedSourceIds.remove($0) },
                isActive: { self.mutedSourceIds.contains($0) },
                activate: { self.mutedSourceIds.insert($0) },
                deactivate: { self.mutedSourceIds.remove($0) }
            ),
            onActivate: {
                if wasFollowing {
                    self.setSourceFollow(sourceId: sourceId, source: trackedSource, following: false)
                }
            },
            onRollbackActivate: {
                if wasFollowing {
                    self.setSourceFollow(sourceId: sourceId, source: trackedSource, following: true)
                }
            }
        )
    }

    func toggleFollowTopic(topicId: String?) async {
        guard let topicId else { return }
        await toggleBookmark(
            id: topicId,
            predicate: "follow",
            entityType: "topic",
            handlers: .init(
                isInFlight: { self.togglingTopicIds.contains($0) },
                begin: { self.togglingTopicIds.insert($0) },
                finish: { self.togglingTopicIds.remove($0) },
                isActive: { self.followedTopicIds.contains($0) },
                activate: { self.followedTopicIds.insert($0) },
                deactivate: { self.followedTopicIds.remove($0) }
            )
        )
    }

    func toggleMuteTopic(topicId: String?) async {
        guard let topicId else { return }
        let wasFollowing = followedTopicIds.contains(topicId)
        await toggleBookmark(
            id: topicId,
            predicate: "mute",
            entityType: "topic",
            handlers: .init(
                isInFlight: { self.togglingMutedTopicIds.contains($0) },
                begin: { self.togglingMutedTopicIds.insert($0) },
                finish: { self.togglingMutedTopicIds.remove($0) },
                isActive: { self.mutedTopicIds.contains($0) },
                activate: { self.mutedTopicIds.insert($0) },
                deactivate: { self.mutedTopicIds.remove($0) }
            ),
            onActivate: {
                if wasFollowing {
                    self.setTopicFollow(topicId: topicId, following: false)
                }
            },
            onRollbackActivate: {
                if wasFollowing {
                    self.setTopicFollow(topicId: topicId, following: true)
                }
            }
        )
    }

    private func setSourceFollow(
        sourceId: String,
        source: RssFeedSource?,
        following: Bool,
        rollbackPosition: SourceRollbackPosition? = nil
    ) {
        if following {
            followedSourceIds.insert(sourceId)
            if yourLoaded, let source, !yourSources.contains(where: { $0.id == sourceId }) {
                if let insertIndex = rollbackIndex(from: rollbackPosition) {
                    yourSources.insert(source, at: insertIndex)
                } else {
                    yourSources.append(source)
                }
            }
        } else {
            followedSourceIds.remove(sourceId)
            yourSources.removeAll { $0.id == sourceId }
        }
    }

    private func setTopicFollow(topicId: String, following: Bool) {
        if following {
            followedTopicIds.insert(topicId)
        } else {
            followedTopicIds.remove(topicId)
        }
    }

    private func toggleBookmark(
        id: String,
        predicate: String,
        entityType: String,
        handlers: BookmarkToggleHandlers,
        onActivate: (() -> Void)? = nil,
        onRollbackActivate: (() -> Void)? = nil
    ) async {
        guard userId != nil, !handlers.isInFlight(id) else { return }
        handlers.begin(id)
        defer { handlers.finish(id) }

        let wasActive = handlers.isActive(id)
        if wasActive {
            handlers.deactivate(id)
        } else {
            handlers.activate(id)
            onActivate?()
        }
        do {
            let endpoint = wasActive
                ? Endpoint.unbookmark(entityType: entityType, entityId: id, predicate: predicate)
                : Endpoint.bookmark(entityType: entityType, entityId: id, predicate: predicate)
            let _: EmptyResponse = try await client.send(endpoint)
        } catch {
            if wasActive {
                handlers.activate(id)
            } else {
                handlers.deactivate(id)
                onRollbackActivate?()
            }
        }
    }
}
