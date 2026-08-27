import VouchaModels

public extension NativeCommentThreadViewModel {
    static func buildTree(
        posts: [Post],
        sort: NativeCommentThreadSort = .new
    ) -> [NativeCommentThreadNode] {
        let postsById = Dictionary(uniqueKeysWithValues: posts.map { ($0.id, $0) })
        let roots = posts.filter { post in
            guard let parentId = post.parentId else { return true }
            return postsById[parentId] == nil
        }
        let childrenByParentId = Dictionary(grouping: postsById.values, by: \.parentId)
        return buildNodes(from: roots, childrenByParentId: childrenByParentId, sort: sort)
    }
}

extension NativeCommentThreadViewModel {
    func rebuildCommentTree() {
        commentTree = Self.buildTree(posts: descendantPosts, sort: sort)
    }

    private static func buildNodes(
        from posts: [Post],
        childrenByParentId: [String?: [Post]],
        sort: NativeCommentThreadSort
    ) -> [NativeCommentThreadNode] {
        let orderedPosts = posts.sorted { compare($0, $1, sort: sort) }
        return orderedPosts.map { post in
            let children = childrenByParentId[post.id] ?? []
            return NativeCommentThreadNode(
                post: post,
                children: buildNodes(from: children, childrenByParentId: childrenByParentId, sort: sort)
            )
        }
    }

    private static func compare(_ lhs: Post, _ rhs: Post, sort: NativeCommentThreadSort) -> Bool {
        switch sort {
        case .new:
            if lhs.createdAt != rhs.createdAt {
                return lhs.createdAt > rhs.createdAt
            }
        case .best:
            let lhsScore = bestScore(for: lhs)
            let rhsScore = bestScore(for: rhs)
            if lhsScore != rhsScore {
                return lhsScore > rhsScore
            }
            if lhs.createdAt != rhs.createdAt {
                return lhs.createdAt > rhs.createdAt
            }
        }
        return lhs.id < rhs.id
    }

    private static func bestScore(for post: Post) -> Int {
        guard let election = post.election else { return 0 }
        return election.votesCountUp - election.votesCountDown
    }
}
