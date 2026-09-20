public extension Endpoint {
    static func postImages(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/posts/\(pathSegment(idOrSlug))/images")
    }

    static func copyrightImageSimilarityCandidates(
        noticeId: String,
        targetId: String
    ) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/copyright-notices/\(pathSegment(noticeId))/targets/\(pathSegment(targetId))" +
                "/image-similarity-candidates"
        )
    }
}
