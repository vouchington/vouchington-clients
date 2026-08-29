import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class EndpointTopicParityTests: XCTestCase {
    func testCreateTopicEndpointUsesExpectedRoute() {
        assertEndpoint(
            Endpoint.createTopic(body: CreateTopicBody(
                name: "Rewards",
                slug: "rewards",
                topicType: "card",
                markdown: "Body",
                hostname: "example.com",
                sourceTopicAliasId: "alias-1"
            )),
            method: .POST,
            path: "/api/v1/topics",
            body: [
                "name": "Rewards",
                "slug": "rewards",
                "topic_type": "card",
                "markdown": "Body",
                "hostname": "example.com",
                "source_topic_alias_id": "alias-1"
            ]
        )
    }

    func testTopicAdditionalHostnameEndpointsUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.topicAdditionalHostnames(topicId: "topic 1"),
            path: "/api/v1/topics/topic%201/additional-hostnames"
        )
        assertEndpoint(
            Endpoint.createTopicAdditionalHostname(topicId: "topic 1", hostname: "example.com"),
            method: .POST,
            path: "/api/v1/topics/topic%201/additional-hostnames",
            body: ["hostname": "example.com"]
        )
        assertEndpoint(
            Endpoint.deleteTopicAdditionalHostname(topicId: "topic 1", hostnameId: "host 1"),
            method: .DELETE,
            path: "/api/v1/topics/topic%201/additional-hostnames/host%201"
        )
    }

    func testTopicAdditionalHostnamesIncludesAfterAndLimit() {
        let endpoint = Endpoint.topicAdditionalHostnames(topicId: "topic-1", after: "cursor-1", limit: 20)
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "after", value: "cursor-1")))
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "limit", value: "20")))
    }

    func testTopicAdditionalHostnamesOmitsAfterAndLimitWhenNil() {
        let endpoint = Endpoint.topicAdditionalHostnames(topicId: "topic-1")
        XCTAssertFalse(endpoint.queryItems.contains(where: { $0.name == "after" }))
        XCTAssertFalse(endpoint.queryItems.contains(where: { $0.name == "limit" }))
    }

    func testTopicAliasesIncludesAfterAndLimit() {
        let endpoint = Endpoint.topicAliases(topicId: "topic-1", after: "cursor-2", limit: 15)
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "after", value: "cursor-2")))
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "limit", value: "15")))
    }

    func testCanonicalHashtagSlugNormalizesMobileSeparators() {
        XCTAssertEqual(CanonicalHashtagSlug(rawValue: " #Me.Too__2026 ")?.value, "me-too-2026")
        XCTAssertEqual(CanonicalHashtagSlug(rawValue: "inner__separators")?.value, "inner-separators")
        XCTAssertNil(CanonicalHashtagSlug(rawValue: "not a hashtag"))
        XCTAssertNil(CanonicalHashtagSlug(rawValue: "-leading"))
        XCTAssertNil(CanonicalHashtagSlug(rawValue: "trailing-"))
        XCTAssertNil(CanonicalHashtagSlug(rawValue: "a\(String(repeating: ".", count: 254))b"))
    }

    func testTopicAdditionalHostnameDecodesBackendHostnameId() throws {
        let json = Data(
            #"""
            {"results":[{"hostname_id":"host-1","hostname":"example.com","topic_id":"topic-1"}],
            "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
            """#.utf8
        )
        let decoded = try makeVouchaDecoder().decode(TopicAdditionalHostnamesResponse.self, from: json)

        XCTAssertEqual(decoded.results.first?.id, "host-1")
        XCTAssertEqual(decoded.results.first?.hostname, "example.com")
        XCTAssertEqual(decoded.pageInfo.hasNextPage, true)
        XCTAssertEqual(decoded.pageInfo.endCursor, "cursor-1")
    }

    func testTopicAdditionalHostnamesResponseMemberwiseInitializer() {
        let hostname = TopicAdditionalHostname(id: "host-2", hostname: "second.example.com", topicId: "topic-1")
        let pageInfo = Page<TopicAdditionalHostname>.PageInfo(hasNextPage: true, endCursor: "cursor-2")

        let response = TopicAdditionalHostnamesResponse(results: [hostname], pageInfo: pageInfo)

        XCTAssertEqual(response.results.map(\.id), ["host-2"])
        XCTAssertEqual(response.pageInfo.hasNextPage, true)
        XCTAssertEqual(response.pageInfo.endCursor, "cursor-2")
    }

    func testTopicSearchDecodesAndPreservesTopicEntityType() throws {
        let json = Data(
            #"""
            {
              "results": [{
                "__entity_type": "topic",
                "id": "topic-1",
                "name": "Rewards",
                "slug": "rewards",
                "topic_type": "rewards_program"
              }],
              "page_info": {
                "has_next_page": false,
                "start_cursor": null,
                "end_cursor": null
              },
              "topics": {}
            }
            """#.utf8
        )

        let decoded = try makeVouchaDecoder().decode(TopicSearchResponse.self, from: json)

        XCTAssertEqual(decoded.results.first?.entityType, "topic")
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try encoder.encode(decoded)
        let jsonObject = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let results = try XCTUnwrap(jsonObject["results"] as? [[String: Any]])
        XCTAssertEqual(results.first?["__entity_type"] as? String, "topic")
    }

    func testTopicImageUploadEndpointsUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.imageUploadUrl(contentType: "image/png", contentLength: 12),
            method: .POST,
            path: "/api/v1/images/upload-url",
            body: ["content_type": "image/png", "content_length": 12]
        )
        assertEndpoint(
            Endpoint.completeImageUpload(imageId: "image 1"),
            method: .POST,
            path: "/api/v1/images/image%201/completions"
        )
        assertEndpoint(
            Endpoint.imageUploadState(imageId: "image 1"),
            path: "/api/v1/images/image%201/upload-state"
        )
    }
}
