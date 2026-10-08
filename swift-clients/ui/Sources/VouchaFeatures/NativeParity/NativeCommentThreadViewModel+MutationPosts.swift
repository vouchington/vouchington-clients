import VouchaAPI
import VouchaModels

extension NativeCommentThreadViewModel {
    func apply(mutationPost post: Post) {
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

    func renderedMutationPost(_ post: Post, client: APIClient) async -> Post {
        guard post.html?.isEmpty != false, let markdown = post.markdown, !markdown.isEmpty else {
            return post
        }
        guard let preview: MarkdownPreviewResponse = try? await client.send(.markdownPreview(markdown: markdown)) else {
            return post
        }
        return post.withRenderedHtml(preview.html)
    }

    func mergedMutationPost(_ mutationPost: Post, preserving existingPost: Post?) -> Post {
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
            provenance: mutationPost.provenance ?? existingPost.provenance,
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
}
