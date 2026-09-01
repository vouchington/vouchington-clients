import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
import VouchaModels
import XCTest

final class EndpointPostAndPasskeyCoverageTests: XCTestCase {
    func testCreatePostEncodesStructuredDataImagesAndTokens() throws {
        let structured = try CreatePostJSONValue.parse(
            jsonString: #"{"flag":true,"count":3,"ratio":1.5,"items":["one",null]}"#
        )
        let endpoint = Endpoint.createPost(
            postType: .dataPoint,
            title: "Native data point",
            markdown: "Native body",
            slug: "native-data-point",
            broadcast: .everyone,
            privacy: .public,
            isAnonymous: true,
            url: "https://example.com",
            urlId: "url-1",
            rootId: "root-1",
            parentId: "parent-1",
            reviewTopicRatings: [.init(topicId: "topic-1", rating: 5)],
            images: [.init(imageId: "image-1", orderIndex: 0, caption: "Hero")],
            dataPointVertical: .creditCard,
            structuredData: structured,
            declaredLanguage: "en",
            turnstileToken: "turnstile-token",
            recaptchaToken: "recaptcha-token",
            idempotencyKey: "00000000-0000-4000-8000-000000000001"
        )

        let body = try encodedJSONObject(from: XCTUnwrap(endpoint.body))

        XCTAssertEqual(endpoint.method, .POST)
        XCTAssertEqual(endpoint.path, "/api/v1/posts")
        XCTAssertEqual(body["post_type"] as? String, "data_point")
        XCTAssertEqual(body["is_anonymous"] as? Bool, true)
        XCTAssertEqual(body["cf_turnstile_response"] as? String, "turnstile-token")
        XCTAssertEqual(body["recaptcha_token"] as? String, "recaptcha-token")
        XCTAssertEqual(endpoint.headers["Idempotency-Key"], "00000000-0000-4000-8000-000000000001")
        XCTAssertEqual((body["images"] as? [[String: Any]])?.first?["caption"] as? String, "Hero")
        let data = try XCTUnwrap(body["structured_data"] as? [String: Any])
        XCTAssertEqual(data["flag"] as? Bool, true)
        XCTAssertEqual(data["count"] as? Int, 3)
        XCTAssertEqual(data["ratio"] as? Double, 1.5)
        XCTAssertEqual((data["items"] as? [Any])?.count, 2)
    }

    func testContributionEndpointsKeepAnExplicitIdempotencyKeyAndDraftIdentity() async throws {
        let identity = ContributionRequestIdentity()
        let first = await identity.key(surface: "post", canonicalIntent: "title\u{001F}body")
        let retry = await identity.key(surface: "post", canonicalIntent: "title\u{001F}body")
        let changed = await identity.key(surface: "post", canonicalIntent: "title\u{001F}changed")
        let endpoint = Endpoint.createPost(
            postType: .discussion,
            title: "Title",
            markdown: "Body",
            idempotencyKey: first
        )

        XCTAssertEqual(first, retry)
        XCTAssertNotEqual(first, changed)
        XCTAssertNotNil(UUID(uuidString: first))
        XCTAssertEqual(endpoint.headers["Idempotency-Key"], first)
        XCTAssertEqual(Endpoint.contributionStatus(action: "discussion").path, "/api/v1/my/contribution-status")
    }

    #if canImport(AuthenticationServices)
        func testPasskeyAssertionResponseEncodesWebAuthnShape() throws {
            let response = PasskeyAuthenticationResponse(
                id: "credential-id",
                rawId: "credential-id",
                response: .init(
                    authenticatorData: "auth-data",
                    clientDataJSON: "client-json",
                    signature: "signature",
                    userHandle: "user-handle"
                )
            )

            let body = try encodedJSONObject(from: response)
            XCTAssertEqual(body["id"] as? String, "credential-id")
            XCTAssertEqual(body["rawId"] as? String, "credential-id")
            XCTAssertEqual(body["type"] as? String, "public-key")
            XCTAssertEqual((body["clientExtensionResults"] as? [String: String])?.isEmpty, true)
            let assertion = try XCTUnwrap(body["response"] as? [String: String])
            XCTAssertEqual(assertion["authenticatorData"], "auth-data")
            XCTAssertEqual(assertion["clientDataJSON"], "client-json")
            XCTAssertEqual(assertion["signature"], "signature")
            XCTAssertEqual(assertion["userHandle"], "user-handle")
        }

        func testPasskeyAuthEndpointsUseExpectedRoutesAndBody() throws {
            XCTAssertEqual(Endpoint.passkeyAuthOptions.method, .POST)
            XCTAssertEqual(Endpoint.passkeyAuthOptions.path, "/api/v1/auth/passkeys/authentication/options")
            XCTAssertEqual(Endpoint.passkeyAuthVerify.path, "/api/v1/auth/passkeys/authentication/verify")

            let response = PasskeyAuthenticationResponse(
                id: "credential-id",
                rawId: "credential-id",
                response: .init(
                    authenticatorData: "auth",
                    clientDataJSON: "client",
                    signature: "sig",
                    userHandle: "user"
                )
            )
            let endpoint = Endpoint.passkeyAuthVerify(response: response)
            let body = try encodedJSONObject(from: XCTUnwrap(endpoint.body))

            XCTAssertEqual(endpoint.method, HTTPMethod.POST)
            XCTAssertEqual(endpoint.path, "/api/v1/auth/passkeys/authentication/verify")
            XCTAssertNotNil(body["response"])
        }
    #endif

    func testCreateCommunityPostUsesCommunityRouteAndEncodesBody() throws {
        let endpoint = Endpoint.createCommunityPost(
            communityIdOrSlug: "native community",
            postType: .review,
            title: "Native community review",
            markdown: "Community body",
            reviewTopicRatings: [.init(topicId: "topic-1", rating: 4)],
            turnstileToken: "turnstile-token",
            idempotencyKey: "00000000-0000-4000-8000-000000000002"
        )

        let body = try encodedJSONObject(from: XCTUnwrap(endpoint.body))

        XCTAssertEqual(endpoint.method, .POST)
        XCTAssertEqual(endpoint.path, "/api/v1/communities/native%20community/posts")
        XCTAssertEqual(body["post_type"] as? String, "review")
        XCTAssertEqual(body["cf_turnstile_response"] as? String, "turnstile-token")
        XCTAssertEqual(endpoint.headers["Idempotency-Key"], "00000000-0000-4000-8000-000000000002")
        XCTAssertEqual((body["review_topic_ratings"] as? [[String: Any]])?.first?["rating"] as? Int, 4)
    }

    func testPostCategoriesEncodeOnCreateCommunityCreateAndUpdateBodies() throws {
        let hashtag = try XCTUnwrap(PostCategoryInput(hashtag: " #Me.Too__2026 "))
        let categories: [PostCategoryInput] = [
            .topic(topicId: " topic-1 "),
            hashtag
        ]

        let createBody = try encodedJSONObject(from: XCTUnwrap(Endpoint.createPost(
            postType: .discussion,
            title: "Discussion",
            markdown: "Body",
            categories: categories,
            idempotencyKey: "00000000-0000-4000-8000-000000000003"
        ).body))
        let communityBody = try encodedJSONObject(from: XCTUnwrap(Endpoint.createCommunityPost(
            communityIdOrSlug: "voucha",
            postType: .discussion,
            title: "Discussion",
            markdown: "Body",
            categories: categories,
            idempotencyKey: "00000000-0000-4000-8000-000000000004"
        ).body))
        let updateBody = try encodedJSONObject(from: XCTUnwrap(Endpoint.updatePost(
            postId: "post-1",
            body: UpdatePostBody(categories: categories)
        ).body))

        for body in [createBody, communityBody, updateBody] {
            let encodedCategories = try XCTUnwrap(body["categories"] as? [[String: Any]])
            XCTAssertEqual(encodedCategories.count, 2)
            XCTAssertEqual(encodedCategories[0]["type"] as? String, "topic")
            XCTAssertEqual(encodedCategories[0]["topic_id"] as? String, "topic-1")
            XCTAssertEqual(encodedCategories[1]["type"] as? String, "hashtag")
            XCTAssertEqual(encodedCategories[1]["hashtag"] as? String, "#Me.Too__2026")
        }
    }

    private func encodedJSONObject(from body: any Encodable) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let data = try encoder.encode(body)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }
}
