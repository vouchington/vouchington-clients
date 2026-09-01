import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension NativeCommentThreadViewModel {
    func toggleSave(postId: String) async {
        guard let client else { return }
        let isSaved = bookmarksByPostId[postId]?["save"] ?? false
        await mutate(operation: {
            if isSaved {
                let _: EmptyResponse = try await client.send(.unbookmark(
                    entityType: "post",
                    entityId: postId,
                    predicate: "save"
                ))
            } else {
                let _: EmptyResponse = try await client.send(.bookmark(
                    entityType: "post",
                    entityId: postId,
                    predicate: "save"
                ))
            }
            setBookmark(postId: postId, predicate: "save", active: !isSaved)
        })
    }

    func report(postId: String, reason: String, note: String?, turnstileToken: String? = nil) async {
        guard let client else { return }
        await mutate(operation: {
            let _: EmptyResponse = try await client.send(.report(
                entityType: "post",
                entityId: postId,
                reason: reason,
                note: note,
                turnstileToken: turnstileToken
            ))
        })
    }

    func edit(postId: String, markdown: String) async {
        guard let client else { return }
        await mutate(operation: {
            let response: CommentThreadPostMutationResponse = try await client.send(.updatePost(
                postId: postId,
                body: UpdatePostBody(markdown: markdown)
            ))
            let post = await renderedMutationPost(response.post, client: client)
            apply(mutationPost: post)
        })
    }

    func delete(postId: String) async {
        guard let client else { return }
        await mutate(reload: true, operation: {
            let _: EmptyResponse = try await client.send(.deletePost(postId: postId))
        })
    }

    func toggleLock(postId: String, lockedAt: Date?) async {
        guard let client else { return }
        await mutate(reload: true, operation: {
            if lockedAt == nil {
                let _: EmptyResponse = try await client.send(.lockPost(postId: postId))
            } else {
                let _: EmptyResponse = try await client.send(.unlockPost(postId: postId))
            }
        })
    }

    func reply(
        parentId: String,
        markdown: String,
        isAnonymous: Bool = false,
        turnstileToken: String? = nil
    ) async {
        guard let client else { return }
        let trimmed = markdown.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        let canonicalIntent = [rootPostId, parentId, trimmed, isAnonymous ? "1" : "0"].joined(separator: "\u{001F}")
        let idempotencyKey = await contributionIdentity.key(surface: "comment", canonicalIntent: canonicalIntent)
        await mutate(operation: {
            let response: CommentThreadPostMutationResponse = try await client.send(.createPost(
                postType: .comment,
                title: "",
                markdown: trimmed,
                isAnonymous: isAnonymous,
                rootId: rootPostId,
                parentId: parentId,
                turnstileToken: turnstileToken,
                idempotencyKey: idempotencyKey
            ))
            await contributionIdentity.complete(surface: "comment", canonicalIntent: canonicalIntent)
            let post = await renderedMutationPost(response.post, client: client)
            apply(mutationPost: post)
        })
    }

    private func reloadCurrentThread() async {
        if let focusedCommentId {
            await loadPermalink(targetCommentId: focusedCommentId)
        } else {
            await loadThread()
        }
    }

    private func setBookmark(postId: String, predicate: String, active: Bool) {
        var bookmarks = bookmarksByPostId[postId] ?? [:]
        bookmarks[predicate] = active
        bookmarksByPostId[postId] = bookmarks
    }

    func mutate(
        reload: Bool = false,
        rollback: @escaping () -> Void = {},
        operation: () async throws -> Void
    ) async {
        mutationState = .loading
        do {
            try await emailVerificationGate.perform(rollbackOnFailure: rollback) {
                try await operation()
            }
            mutationState = .loaded
            if reload {
                await reloadCurrentThread()
            }
        } catch let error as ContributionAdmissionFailure {
            mutationState = .error(.api(statusCode: error.statusCode, preconditionCode: error.code))
        } catch let error as VouchaError {
            mutationState = .error(error)
        } catch {
            mutationState = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }
}

private struct CommentThreadPostMutationResponse: Decodable {
    let post: Post
}
