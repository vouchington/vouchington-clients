import Foundation
import VouchaModels

public struct LandingPageReview: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let title: String
    public let slug: String?
    public let markdown: String
    public let createdAt: Date
    public let reviewTopicRatings: [LandingPageReviewTopicRating]
    @RequiredNullable public var declaredLanguage: String?
    @RequiredNullable public var linguaRsDetectedLanguage: String?
}
