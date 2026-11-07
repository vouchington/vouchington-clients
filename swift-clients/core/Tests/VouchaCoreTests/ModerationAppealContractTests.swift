import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationAppealContractTests: XCTestCase {
    func testAppealContextsDecodeFromSharedFixturesAndRemainOptionalForOldResponses() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        let response = try makeVouchaDecoder().decode(ModerationAppealEnvelope.self, from: data)
        let appeal = try XCTUnwrap(Optional(response.appeal))

        let targetContext = try XCTUnwrap(appeal.targetContext)
        let encodedContext = try JSONEncoder.vouchaFixtureEncoder.encode(targetContext)
        let context = try XCTUnwrap(
            JSONSerialization.jsonObject(with: encodedContext) as? [String: Any]
        )
        XCTAssertEqual(context["type"] as? String, "post_removal")
        XCTAssertEqual(context["id"] as? String, "post-1")
        XCTAssertEqual(context["title"] as? String, "A removed review")
        XCTAssertEqual(appeal.staffContext?.appellant.username, "appealing-reviewer")
        XCTAssertEqual(
            appeal.staffContext?.originalDecision.internalReason,
            "The post did not meet the content policy."
        )

        var oldObject = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var oldAppeal = try XCTUnwrap(oldObject["appeal"] as? [String: Any])
        oldAppeal.removeValue(forKey: "target_context")
        oldAppeal.removeValue(forKey: "staff_context")
        oldObject["appeal"] = oldAppeal
        let oldData = try JSONSerialization.data(withJSONObject: oldObject)
        let oldResponse = try makeVouchaDecoder().decode(ModerationAppealEnvelope.self, from: oldData)
        XCTAssertNil(oldResponse.appeal.targetContext)
        XCTAssertNil(oldResponse.appeal.staffContext)
    }

    func testAppealTargetContextsRoundTripCommunityBanAndSuspension() throws {
        let source = [
            """
            {
              "type":"community_ban",
              "id":"ban-1",
              "community":{"id":"community-1","name":"Community"},
              "reason":null,
              "expires_at":null,
              "created_at":"2026-07-01T09:00:00Z"
            }
            """,
            """
            {
              "type":"suspension",
              "id":"suspension-1",
              "reason":"Repeated policy violations",
              "created_at":"2026-07-02T09:00:00Z"
            }
            """
        ]
        let decoder = makeVouchaDecoder()

        let roundTripped = try source.map { json in
            let context = try decoder.decode(
                ModerationAppealTargetContext.self,
                from: Data(json.utf8)
            )
            let encoded = try JSONEncoder.vouchaFixtureEncoder.encode(context)
            return try XCTUnwrap(
                JSONSerialization.jsonObject(with: encoded) as? [String: Any]
            )
        }

        XCTAssertEqual(roundTripped.map { $0["type"] as? String }, ["community_ban", "suspension"])
        XCTAssertEqual(roundTripped[0]["id"] as? String, "ban-1")
        XCTAssertEqual(roundTripped[1]["reason"] as? String, "Repeated policy violations")
    }

    func testAppealTargetContextRejectsUnknownType() {
        let data = Data(#"{"type":"unsupported"}"#.utf8)

        XCTAssertThrowsError(
            try makeVouchaDecoder().decode(ModerationAppealTargetContext.self, from: data)
        )
    }

    func testCanonicalAppealReasonsExposeAllFiveWireValues() {
        XCTAssertEqual(
            ModerationAppealReason.allCases.map(\.rawValue),
            ["incorrect_facts", "wrong_rule", "context_missing", "disproportionate", "other"]
        )
    }

    func testSuspensionSubmissionOmitsTargetIdAndEncodesCanonicalReason() {
        let request = ModerationAppealSubmissionRequest(
            targetType: .suspension,
            reason: .contextMissing,
            details: "The decision missed important context."
        )
        assertEndpoint(
            Endpoint.submitAppeal(request),
            method: .POST,
            path: "/api/v1/appeals",
            body: [
                "target_type": "suspension",
                "appeal_reason": "[context_missing] The decision missed important context."
            ]
        )
    }

    func testAppealSubmissionResponsePreservesDuplicateResult() throws {
        let listData = ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        let listObject = try XCTUnwrap(
            JSONSerialization.jsonObject(with: listData) as? [String: Any]
        )
        var appeal = try XCTUnwrap(listObject["appeal"] as? [String: Any])
        appeal.removeValue(forKey: "staff_context")
        XCTAssertNil(appeal["staff_context"])
        let data = try JSONSerialization.data(withJSONObject: [
            "appeal": appeal,
            "isDuplicate": true
        ])

        let response = try makeVouchaDecoder().decode(ModerationAppealSubmissionResponse.self, from: data)
        XCTAssertTrue(response.isDuplicate)
        XCTAssertEqual(response.appeal.id, "00000000-0000-7000-8000-000000000102")
        XCTAssertNil(response.appeal.staffContext)
    }
}

private extension JSONEncoder {
    static var vouchaFixtureEncoder: JSONEncoder {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return encoder
    }
}
