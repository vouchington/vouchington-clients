import VouchaModels

extension Post {
    func withRenderedHtml(_ renderedHtml: String?) -> Post {
        hydrated(
            renderedHtml: renderedHtml,
            metrics: metrics,
            election: election,
            voteChoice: nil
        )
    }

    func hydrated(
        renderedHtml: String?,
        metrics: PostMetrics?,
        election: PostElection?,
        voteChoice: ElectionVoteChoice?
    ) -> Post {
        let hydratedElection = election.map {
            PostElection(
                votesScoreNet: $0.votesScoreNet,
                votesCountUp: $0.votesCountUp,
                votesCountDown: $0.votesCountDown,
                myVote: voteChoice ?? $0.myVote
            )
        }
        return Post(
            id: id,
            slug: slug,
            postType: postType,
            title: title.flatMap { $0.isEmpty ? nil : $0 },
            markdown: markdown,
            html: renderedHtml ?? html,
            parentId: parentId,
            rootId: rootId,
            createdById: createdById,
            createdAt: createdAt,
            provenance: provenance,
            broadcast: broadcast,
            privacy: privacy,
            isAnonymous: isAnonymous,
            communityId: communityId,
            clearanceStatus: clearanceStatus,
            metrics: metrics ?? self.metrics,
            election: hydratedElection ?? self.election,
            createdBy: createdBy,
            updatedAt: updatedAt,
            deletedAt: deletedAt,
            deletedById: deletedById,
            lockedAt: lockedAt,
            lockedById: lockedById,
            canEditContent: canEditContent,
            canDelete: canDelete,
            canLock: canLock,
            postExplicitCategories: postExplicitCategories,
            postHashtags: postHashtags
        )
    }
}
