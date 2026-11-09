import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

enum IntegrityTestSupport {
    static func reportFlag(
        id: String = "report-1",
        postId: String? = "post-1",
        userId: String? = nil,
        hostnameId: String? = nil,
        rssFeedItemId: String? = nil,
        resolution: String? = nil
    ) throws -> ReportIntegrityFlag {
        try canonicalFlag("report", overrides: [
            "id": id, "post_id": postId as Any? ?? NSNull(), "reported_user_id": userId as Any? ?? NSNull(),
            "hostname_id": hostnameId as Any? ?? NSNull(), "rss_feed_item_id": rssFeedItemId as Any? ?? NSNull(),
            "reporter_count": 5, "new_account_reporter_percent": 0.6,
            "details": ["window_minutes": 30, "evidence": "captured"],
            "resolved_at": resolution == nil ? NSNull() : "2026-07-01T12:00:00.000Z" as Any,
            "resolved_by_id": resolution == nil ? NSNull() : "admin-1" as Any,
            "resolver_username": NSNull(), "resolver_display_name": NSNull(),
            "resolution": resolution as Any? ?? NSNull()
        ])
    }

    static func voteFlag(
        id: String = "vote-1",
        postId: String? = "post-1",
        topicId: String? = nil,
        hostnameId: String? = nil,
        rssFeedItemId: String? = nil,
        entityRelationId: String? = nil,
        agentModerationId: String? = nil,
        resolution: String? = nil
    ) throws -> VoteIntegrityFlag {
        try canonicalFlag("vote", overrides: [
            "id": id, "post_id": postId as Any? ?? NSNull(), "topic_id": topicId as Any? ?? NSNull(),
            "hostname_id": hostnameId as Any? ?? NSNull(), "rss_feed_item_id": rssFeedItemId as Any? ?? NSNull(),
            "entity_relation_id": entityRelationId as Any? ?? NSNull(),
            "agent_moderation_id": agentModerationId as Any? ?? NSNull(),
            "details": ["vote_count": 20, "evidence": "captured"],
            "resolved_at": resolution == nil ? NSNull() : "2026-07-01T12:00:00.000Z" as Any,
            "resolved_by_id": resolution == nil ? NSNull() : "admin-1" as Any,
            "resolver_username": NSNull(), "resolver_display_name": NSNull(),
            "resolution": resolution as Any? ?? NSNull()
        ])
    }

    private static func canonicalFlag<Value: Decodable>(
        _ domain: String, overrides: [String: Any]
    ) throws -> Value {
        let data = ApiFixtureLoader.data("native.moderation.\(domain)-integrity.default")
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var flag = try XCTUnwrap((object["results"] as? [[String: Any]])?.first)
        flag.merge(overrides) { _, new in new }
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            Value.self, from: JSONSerialization.data(withJSONObject: flag)
        )
    }

    static func reportPage(
        _ flags: [ReportIntegrityFlag],
        hasMore: Bool = false,
        cursor: String? = nil
    ) throws -> ReportIntegrityFlagsResponse {
        try page(flags, hasMore: hasMore, cursor: cursor)
    }

    static func votePage(
        _ flags: [VoteIntegrityFlag],
        hasMore: Bool = false,
        cursor: String? = nil
    ) throws -> VoteIntegrityFlagsResponse {
        try page(flags, hasMore: hasMore, cursor: cursor)
    }

    static func reportPenalty(
        flag: ReportIntegrityFlag,
        count: Int = 2
    ) throws -> ReportIntegrityPenaltyResponse {
        let flagObject = try jsonObject(flag)
        return try decode("""
        {
          "penalized_user_count":\(count),
          "penalties":[{"id":"penalty-1","user_id":"user-1"}],
          "flag":\(flagObject)
        }
        """)
    }

    static func votePenalty(
        count: Int = 2,
        flag: VoteIntegrityFlag? = nil
    ) throws -> VoteIntegrityPenaltyResponse {
        let flag = try flag ?? voteFlag()
        let flagObject = try jsonObject(flag)
        return try decode(#"{"penalized_user_count":\#(count),"flag":\#(flagObject)}"#)
    }

    private static func page<Response: Decodable>(
        _ flags: [some Encodable],
        hasMore: Bool,
        cursor: String?
    ) throws -> Response {
        let data = try JSONEncoder.vouchaTestEncoder.encode(flags)
        let array = String(decoding: data, as: UTF8.self)
        return try decode("""
        {
          "results":\(array),
          "page_info":{
            "has_next_page":\(hasMore),
            "end_cursor":\(json(cursor)),
            "start_cursor":null
          }
        }
        """)
    }

    private static func decode<Value: Decodable>(_ json: String) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: Data(json.utf8))
    }

    private static func jsonObject(_ value: some Encodable) throws -> String {
        try String(decoding: JSONEncoder.vouchaTestEncoder.encode(value), as: UTF8.self)
    }

    private static func json(_ value: String?) -> String {
        value.map { "\"\($0)\"" } ?? "null"
    }
}

private extension JSONEncoder {
    static var vouchaTestEncoder: JSONEncoder {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            try container.encode(date.formatted(
                Date.ISO8601FormatStyle(includingFractionalSeconds: true)
            ))
        }
        return encoder
    }
}

@MainActor
final class ReportIntegrityServiceDouble: ReportIntegrityServicing {
    struct Page {
        let result: Result<ReportIntegrityFlagsResponse, Error>
        let delay: Duration
    }

    var pages: [Page] = []
    var dismissResults: [Result<ReportIntegrityFlag, Error>] = []
    var exactResults: [Result<ReportIntegrityFlag, Error>] = []
    var penaltyResults: [Result<ReportIntegrityPenaltyResponse, Error>] = []
    var dismissDelay: Duration = .zero
    var penaltyDelay: Duration = .zero
    var calls: [(IntegrityFlagStatus?, String?, Int)] = []
    var dismissCalls: [String] = []
    var exactCalls: [String] = []
    var penaltyCalls: [String] = []

    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> ReportIntegrityFlagsResponse {
        calls.append((status, after, limit))
        let page = pages.removeFirst()
        if page.delay > .zero {
            try await Task.sleep(for: page.delay)
        }
        return try page.result.get()
    }

    func dismiss(flagId: String) async throws -> ReportIntegrityFlag {
        dismissCalls.append(flagId)
        if dismissDelay > .zero {
            try await Task.sleep(for: dismissDelay)
        }
        return try dismissResults.removeFirst().get()
    }

    func flag(id: String) async throws -> ReportIntegrityFlag {
        exactCalls.append(id)
        return try exactResults.removeFirst().get()
    }

    func penalizeReporters(flagId: String) async throws -> ReportIntegrityPenaltyResponse {
        penaltyCalls.append(flagId)
        if penaltyDelay > .zero {
            try await Task.sleep(for: penaltyDelay)
        }
        return try penaltyResults.removeFirst().get()
    }
}

@MainActor
final class VoteIntegrityServiceDouble: VoteIntegrityServicing {
    struct Page {
        let result: Result<VoteIntegrityFlagsResponse, Error>
        let delay: Duration
    }

    var pages: [Page] = []
    var resolutionResults: [Result<VoteIntegrityFlag, Error>] = []
    var exactResults: [Result<VoteIntegrityFlag, Error>] = []
    var resolutionResultsByFlagId: [String: [Result<VoteIntegrityFlag, Error>]] = [:]
    var penaltyResults: [Result<VoteIntegrityPenaltyResponse, Error>] = []
    var penaltyIdResults: [Result<Set<String>, Error>] = []
    var resolutionDelay: Duration = .zero
    var penaltyDelay: Duration = .zero
    var calls: [(IntegrityFlagStatus?, String?, Int)] = []
    var resolutionCalls: [(String, VoteIntegrityResolution)] = []
    var exactCalls: [String] = []
    var penaltyCalls: [String] = []
    var penaltyIdCalls: [String] = []
    var operationCalls: [String] = []

    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> VoteIntegrityFlagsResponse {
        calls.append((status, after, limit))
        let page = pages.removeFirst()
        if page.delay > .zero {
            try await Task.sleep(for: page.delay)
        }
        return try page.result.get()
    }

    func resolve(flagId: String, resolution: VoteIntegrityResolution) async throws -> VoteIntegrityFlag {
        resolutionCalls.append((flagId, resolution))
        if resolutionDelay > .zero {
            try await Task.sleep(for: resolutionDelay)
        }
        if var results = resolutionResultsByFlagId[flagId], !results.isEmpty {
            let result = results.removeFirst()
            resolutionResultsByFlagId[flagId] = results
            return try result.get()
        }
        return try resolutionResults.removeFirst().get()
    }

    func flag(id: String) async throws -> VoteIntegrityFlag {
        exactCalls.append(id)
        operationCalls.append("flag:\(id)")
        return try exactResults.removeFirst().get()
    }

    func applyPenalty(flagId: String) async throws -> VoteIntegrityPenaltyResponse {
        penaltyCalls.append(flagId)
        operationCalls.append("apply-penalty:\(flagId)")
        if penaltyDelay > .zero {
            try await Task.sleep(for: penaltyDelay)
        }
        return try penaltyResults.removeFirst().get()
    }

    func penaltyIds(flagId: String) async throws -> Set<String> {
        penaltyIdCalls.append(flagId)
        operationCalls.append("penalty-ids:\(flagId)")
        return try penaltyIdResults.removeFirst().get()
    }
}
