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

    private func apply(mutationPost post: Post) {
        var didMergeExistingPost = false
        if post.id == rootPostId || post.id == rootPost?.id {
            rootPost = mergedMutationPost(post, preserving: rootPost)
            didMergeExistingPost = true
        }
        if let index = descendantPosts.firstIndex(where: { $0.id == post.id }) {
            descendantPosts[index] = mergedMutationPost(post, preserving: descendantPosts[index])
            didMergeExistingPost = true
        }
        if let index = ancestorPosts.firstIndex(where: { $0.id == post.id }) {
            ancestorPosts[index] = mergedMutationPost(post, preserving: ancestorPosts[index])
            didMergeExistingPost = true
        }
        if !didMergeExistingPost {
            descendantPosts.append(newReplyMutationPost(post))
        }
        descendantPagination.replaceItems(descendantPosts)
        rebuildCommentTree()
    }

    private func renderedMutationPost(_ post: Post, client: APIClient) async -> Post {
        guard post.html?.isEmpty != false, let markdown = post.markdown, !markdown.isEmpty else {
            return post
        }
        guard let preview: MarkdownPreviewResponse = try? await client.send(.markdownPreview(markdown: markdown)) else {
            return post
        }
        return post.withRenderedHtml(preview.html)
    }

    private func mergedMutationPost(_ mutationPost: Post, preserving existingPost: Post?) -> Post {
        guard let existingPost else { return mutationPost }
        return Post(
            id: mutationPost.id,
            slug: mutationPost.slug ?? existingPost.slug,
            postType: mutationPost.postType,
            title: mutationPost.title,
            markdown: mutationPost.markdown,
            html: mutationPost.html,
            parentId: mutationPost.parentId,
            rootId: mutationPost.rootId,
            createdById: mutationPost.createdById,
            createdAt: mutationPost.createdAt,
            broadcast: mutationPost.broadcast ?? existingPost.broadcast,
            privacy: mutationPost.privacy,
            isAnonymous: mutationPost.isAnonymous,
            communityId: mutationPost.communityId ?? existingPost.communityId,
            clearanceStatus: mutationPost.clearanceStatus ?? existingPost.clearanceStatus,
            metrics: mutationPost.metrics ?? existingPost.metrics,
            election: mutationPost.election ?? existingPost.election,
            createdBy: mutationPost.createdBy ?? existingPost.createdBy,
            updatedAt: mutationPost.updatedAt ?? existingPost.updatedAt,
            deletedAt: mutationPost.deletedAt ?? existingPost.deletedAt,
            deletedById: mutationPost.deletedById ?? existingPost.deletedById,
            lockedAt: mutationPost.lockedAt ?? existingPost.lockedAt,
            lockedById: mutationPost.lockedById ?? existingPost.lockedById,
            canEditContent: mutationPost.canEditContent ?? existingPost.canEditContent,
            canDelete: mutationPost.canDelete ?? existingPost.canDelete,
            canLock: mutationPost.canLock ?? existingPost.canLock,
            postExplicitCategories: mutationPost.postExplicitCategories ?? existingPost.postExplicitCategories,
            postHashtags: mutationPost.postHashtags ?? existingPost.postHashtags
        )
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
