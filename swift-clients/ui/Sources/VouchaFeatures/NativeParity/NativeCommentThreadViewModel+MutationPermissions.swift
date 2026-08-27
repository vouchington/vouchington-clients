import VouchaModels

extension NativeCommentThreadViewModel {
    func newReplyMutationPost(_ mutationPost: Post) -> Post {
        Post(
            id: mutationPost.id,
            slug: mutationPost.slug,
            postType: mutationPost.postType,
            title: mutationPost.title,
            markdown: mutationPost.markdown,
            html: mutationPost.html,
            parentId: mutationPost.parentId,
            rootId: mutationPost.rootId,
            createdById: mutationPost.createdById,
            createdAt: mutationPost.createdAt,
            broadcast: mutationPost.broadcast,
            privacy: mutationPost.privacy,
            isAnonymous: mutationPost.isAnonymous,
            communityId: mutationPost.communityId,
            clearanceStatus: mutationPost.clearanceStatus,
            metrics: mutationPost.metrics,
            election: mutationPost.election,
            createdBy: mutationPost.createdBy,
            updatedAt: mutationPost.updatedAt,
            deletedAt: mutationPost.deletedAt,
            deletedById: mutationPost.deletedById,
            lockedAt: mutationPost.lockedAt,
            lockedById: mutationPost.lockedById,
            canEditContent: mutationPost.canEditContent ?? true,
            canDelete: mutationPost.canDelete ?? true,
            canLock: mutationPost.canLock ?? rootPost?.canLock ?? false
        )
    }
}
