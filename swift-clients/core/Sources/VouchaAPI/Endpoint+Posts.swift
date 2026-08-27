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
    let images: [CreatePostImageInput]?
    let dataPointVertical: DataPointVertical?
    let structuredData: CreatePostJSONValue?
    let declaredLanguage: String?
    let cfTurnstileResponse: String?
    let recaptchaToken: String?
}

public extension Endpoint {
    static func post(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/posts/\(pathSegment(idOrSlug))")
    }

    static func postDescendants(postId: String, after: String? = nil, limit: Int = 100) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/posts/\(pathSegment(postId))/descendants",
            queryItems: queryItems
        )
    }

    static func postAncestors(postId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/posts/\(pathSegment(postId))/ancestors")
    }

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
        images: [CreatePostImageInput]? = nil,
        dataPointVertical: DataPointVertical? = nil,
        structuredData: CreatePostJSONValue? = nil,
        declaredLanguage: String? = nil,
        turnstileToken: String? = nil,
        recaptchaToken: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts",
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

private func jsonValue(from value: Any) throws -> CreatePostJSONValue {
    switch value {
    case is NSNull:
        return .null
    case let string as String:
        return .string(string)
    case let number as NSNumber:
        if isBooleanJSONNumber(number) {
            return .bool(number.boolValue)
        }
        if number.doubleValue.rounded() == number.doubleValue {
            return .integer(number.intValue)
        }
        return .number(number.doubleValue)
    case let array as [Any]:
        return try .array(array.map(jsonValue(from:)))
    case let object as [String: Any]:
        return try .object(object.mapValues(jsonValue(from:)))
    default:
        throw NSError(
            domain: "CreatePostJSONValue",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: "Unsupported JSON value: \(value)"]
        )
    }
}

private func isBooleanJSONNumber(_ number: NSNumber) -> Bool {
    #if canImport(CoreFoundation)
        return CFGetTypeID(number) == CFBooleanGetTypeID()
    #else
        let type = String(cString: number.objCType)
        return type == "c" || type == "B"
    #endif
}
