public extension LandingPageReview {
    static func == (lhs: Self, rhs: Self) -> Bool {
        lhs.id == rhs.id
            && lhs.title == rhs.title
            && lhs.slug == rhs.slug
            && lhs.markdown == rhs.markdown
            && lhs.createdAt == rhs.createdAt
            && lhs.reviewTopicRatings == rhs.reviewTopicRatings
            && lhs.declaredLanguage == rhs.declaredLanguage
            && lhs.linguaRsDetectedLanguage == rhs.linguaRsDetectedLanguage
    }
}
