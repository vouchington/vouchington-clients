import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class PublicAppealProjectionTests: XCTestCase {
    @MainActor
    func testCanonicalPublicDetailUsesRealTypedAPIClientRoute() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        let host = "public-appeal-\(UUID().uuidString.lowercased()).example.test"
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://\(host)"))),
            cookieStorage: jar,
            protocolClasses: [PublicAppealReadURLProtocol.self],
            bootstrapSession: false,
            sessionConfiguration: configuration
        )
        let response: MemberAppealReadResponse = try await client.send(
            .appeal(id: "00000000-0000-7000-8000-000000000102")
        )
        guard case let .member(appeal) = response.appeal else {
            return XCTFail("Member detail must preserve its redacted projection")
        }
        XCTAssertEqual(appeal.id, "00000000-0000-7000-8000-000000000102")
        XCTAssertEqual(appeal.postId, "post-1")
        XCTAssertNil(appeal.publicResponse)
    }

    func testPublicReadAndSubmissionPreserveExactPrivacyKeysets() throws {
        for id in [
            "native.moderation.appeals.detail.member.default",
            "native.moderation.appeals.detail.member.sent",
            "native.moderation.appeals.create.member.default"
        ] {
            let data = ApiFixtureLoader.data(id)
            let response = try makeVouchaDecoder().decode(MemberAppealReadResponse.self, from: data)
            guard case .member = response.appeal else { return XCTFail(id) }
            let original = try appealObject(data)
            let encoder = JSONEncoder()
            encoder.keyEncodingStrategy = .convertToSnakeCase
            encoder.dateEncodingStrategy = .iso8601
            let encoded = try appealObject(encoder.encode(response))
            XCTAssertEqual(Set(original.keys), Set(encoded.keys), id)
            XCTAssertTrue(Self.protectedFields.isDisjoint(with: Set(encoded.keys)), id)
        }
        let created = try makeVouchaDecoder().decode(
            MemberAppealSubmissionResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.create.member.default")
        )
        XCTAssertFalse(created.isDuplicate)
        XCTAssertEqual(created.appeal.status, .pending)
        let submissionEncoder = JSONEncoder()
        submissionEncoder.keyEncodingStrategy = .convertToSnakeCase
        submissionEncoder.dateEncodingStrategy = .iso8601
        let submissionData = try submissionEncoder.encode(created)
        let submission = try XCTUnwrap(JSONSerialization.jsonObject(with: submissionData) as? [String: Any])
        XCTAssertEqual(Set(submission.keys), ["appeal", "isDuplicate"])
        XCTAssertEqual(submission["isDuplicate"] as? Bool, false)
        let roundTrip = try makeVouchaDecoder().decode(MemberAppealSubmissionResponse.self, from: submissionData)
        XCTAssertFalse(roundTrip.isDuplicate)
        XCTAssertEqual(roundTrip.appeal.id, created.appeal.id)
    }

    func testEveryProtectedKeyIncludingNullSelectsStrictStaffDecode() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.detail.member.default")
        let publicAppeal = try appealObject(data)
        for key in Self.protectedFields {
            var appeal = publicAppeal
            appeal[key] = NSNull()
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                MemberAppealReadResponse.self,
                from: JSONSerialization.data(withJSONObject: ["appeal": appeal])
            ), key)
        }
    }

    func testMemberLifecycleRetainsSentOnlyPublicResponseAndIdentity() throws {
        let decoder = makeVouchaDecoder()
        let pending = try decoder.decode(
            MemberAppealReadResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.detail.member.default")
        ).appeal
        let sent = try decoder.decode(
            MemberAppealReadResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.detail.member.sent")
        ).appeal
        XCTAssertEqual(pending.id, sent.id)
        XCTAssertNil(pending.publicResponse)
        XCTAssertNil(pending.sentAt)
        XCTAssertNotNil(sent.publicResponse)
        XCTAssertNotNil(sent.sentAt)
        XCTAssertEqual(sent.status, .resolved)
    }

    func testPublicRequiredFieldsAndNullablePresenceStayStrict() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.detail.member.default")
        let original = try appealObject(data)
        for key in [
            "id", "status", "created_at", "updated_at", "user_warning_id", "user_suspension_id",
            "community_ban_id", "post_id", "community_id", "post_removal_kind", "public_response",
            "approved_at", "sent_at", "resolved_at", "resolution_action"
        ] {
            var appeal = original
            appeal.removeValue(forKey: key)
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                MemberAppealReadResponse.self,
                from: JSONSerialization.data(withJSONObject: ["appeal": appeal])
            ), key)
        }
        var optionalOmission = original
        optionalOmission.removeValue(forKey: "is_overdue")
        optionalOmission.removeValue(forKey: "target_context")
        let response = try makeVouchaDecoder().decode(
            MemberAppealReadResponse.self,
            from: JSONSerialization.data(withJSONObject: ["appeal": optionalOmission])
        )
        guard case let .member(appeal) = response.appeal else { return XCTFail("Expected member") }
        XCTAssertNil(appeal.isOverdue)
        XCTAssertNil(appeal.targetContext)
    }

    private func appealObject(_ data: Data) throws -> [String: Any] {
        let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        return try XCTUnwrap(envelope["appeal"] as? [String: Any])
    }

    static let protectedFields: Set<String> = [
        "case_id", "appellant_user_id", "appeal_reason", "recommended_action",
        "ai_public_response", "ai_internal_response", "model", "ai_drafted_at",
        "internal_notes", "drafted_at", "edited_at", "edited_by_id", "approved_by_id",
        "resolved_by_id", "latest_lifecycle_change_id", "staff_context",
        "ai_draft_requested_run_id", "ai_draft_run_id"
    ]
}
