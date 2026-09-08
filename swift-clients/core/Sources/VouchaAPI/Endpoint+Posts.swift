import Foundation
#if canImport(CoreFoundation)
    import CoreFoundation
#endif
import VouchaModels

public enum CreatePostJSONValue: Encodable, Sendable {
    case string(String)
    case number(Double)
    case integer(Int)
    case bool(Bool)
    case null
    case array([CreatePostJSONValue])
    case object([String: CreatePostJSONValue])

    public static func parse(jsonString: String) throws -> CreatePostJSONValue {
        let data = Data(jsonString.utf8)
        let object = try JSONSerialization.jsonObject(with: data)
        return try jsonValue(from: object)
    }

    public func encode(to encoder: any Encoder) throws {
        switch self {
        case let .string(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .number(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .integer(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .bool(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case .null:
            var container = encoder.singleValueContainer()
            try container.encodeNil()
        case let .array(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .object(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        }
    }
}

public struct CreatePostImageInput: Encodable, Sendable {
    public let imageId: String
    public let orderIndex: Int
    public let caption: String?

    public init(imageId: String, orderIndex: Int, caption: String? = nil) {
        self.imageId = imageId
        self.orderIndex = orderIndex
        self.caption = caption
    }
}

public struct CreatePostReviewTopicRatingInput: Encodable, Sendable {
    public let topicId: String
    public let rating: Int

    public init(topicId: String, rating: Int) {
        self.topicId = topicId
        self.rating = rating
    }
}

struct CreatePostBody: Encodable {
    let postType: PostType
    let title: String
    let markdown: String
    let slug: String?
    let broadcast: BroadcastScope
    let privacy: PostPrivacy
    let isAnonymous: Bool
    let url: String?
    let urlId: String?
    let rootId: String?
    let parentId: String?
    let reviewTopicRatings: [CreatePostReviewTopicRatingInput]?
    let categories: [PostCategoryInput]?
    let images: [CreatePostImageInput]?
    let dataPointVertical: DataPointVertical?
    let structuredData: CreatePostJSONValue?
    let declaredLanguage: String?
    let cfTurnstileResponse: String?
    let recaptchaToken: String?
}

public extension Endpoint {
    static func createPost(
        postType: PostType,
        title: String,
        markdown: String,
        slug: String? = nil,
        broadcast: BroadcastScope = .everyone,
        privacy: PostPrivacy = .public,
        isAnonymous: Bool = false,
        url: String? = nil,
        urlId: String? = nil,
        rootId: String? = nil,
        parentId: String? = nil,
        reviewTopicRatings: [CreatePostReviewTopicRatingInput]? = nil,
        categories: [PostCategoryInput]? = nil,
        images: [CreatePostImageInput]? = nil,
        dataPointVertical: DataPointVertical? = nil,
        structuredData: CreatePostJSONValue? = nil,
        declaredLanguage: String? = nil,
        turnstileToken: String? = nil,
        recaptchaToken: String? = nil,
        idempotencyKey: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts",
            headers: ["Idempotency-Key": idempotencyKey],
            body: CreatePostBody(
                postType: postType,
                title: title,
                markdown: markdown,
                slug: slug,
                broadcast: broadcast,
                privacy: privacy,
                isAnonymous: isAnonymous,
                url: url,
                urlId: urlId,
                rootId: rootId,
                parentId: parentId,
                reviewTopicRatings: reviewTopicRatings,
                categories: categories,
                images: images,
                dataPointVertical: dataPointVertical,
                structuredData: structuredData,
                declaredLanguage: declaredLanguage,
                cfTurnstileResponse: turnstileToken,
                recaptchaToken: recaptchaToken
            )
        )
    }

}
