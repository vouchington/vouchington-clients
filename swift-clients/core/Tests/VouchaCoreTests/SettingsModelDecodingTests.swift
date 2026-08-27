import Foundation
@testable import VouchaModels
import XCTest

final class SettingsModelDecodingTests: XCTestCase {
    func testEmailPreferencesDecodeNullModerationTimezone() throws {
        let response = try makeVouchaDecoder().decode(
            EmailPreferencesResponse.self,
            from: Data(
                #"{"email_preferences":{"engagement_emails_enabled":true,"news_digest_frequency":"weekly","moderation_emails_enabled":true,"community_digest_frequency":"weekly","moderation_email_cadence":"daily","moderation_email_days_of_week":[1,2,3,4,5],"moderation_email_time_of_day":"09:00","moderation_email_timezone":null}}"#
                    .utf8
            )
        )

        XCTAssertNil(response.emailPreferences.moderationEmailTimezone)
    }

    func testApiKeyAndExportModelsDecode() throws {
        let decoder = makeVouchaDecoder()

        let apiKey = try decoder.decode(
            ApiKeyCreationResponse.self,
            from: Data(
                #"{"api_key":{"id":"key-1","user_id":"user-1","prefix":"voucha_rss_abcd","type":"rss","label":"Reader","permissions":["rss-feeds:read"],"created_at":"2026-03-01T10:00:00Z","last_used_at":null,"revoked_at":null,"updated_at":"2026-03-01T10:00:00Z"},"raw_key":"voucha_rss_raw"}"#
                    .utf8
            )
        )
        XCTAssertEqual(apiKey.apiKey.prefix, "voucha_rss_abcd")
        XCTAssertEqual(apiKey.rawKey, "voucha_rss_raw")

        let request = try decoder.decode(
            UserDataRequest.self,
            from: Data(
                #"{"id":"req-1","user_id":"user-1","queued_at":"2026-03-01T10:00:00Z","processing_started_at":null,"completed_at":null,"failed_at":null,"expires_at":null,"created_at":"2026-03-01T10:00:00Z","updated_at":"2026-03-01T10:00:00Z","status":"pending","download_url":null}"#
                    .utf8
            )
        )
        XCTAssertEqual(request.status, .pending)
        XCTAssertNil(request.downloadURL)
    }

    func testMembershipProfileLinkAndPushModelsDecode() throws {
        let decoder = makeVouchaDecoder()

        let sessions = try decoder.decode(
            Page<AuthSession>.self,
            from: Data(
                #"{"results":[{"id":"session-1","device_id":"device-1","device_name":"MacBook Pro","user_agent":"Safari","ip_address":"203.0.113.8","created_at":"2026-03-01T10:00:00Z","last_seen_at":"2026-03-01T12:00:00Z","expires_at":"2026-03-31T10:00:00Z","is_current":true}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                    .utf8
            )
        )
        XCTAssertEqual(sessions.results.first?.deviceName, "MacBook Pro")
        XCTAssertEqual(sessions.results.first?.isCurrent, true)

        let membership = try decoder.decode(
            MembershipResponse.self,
            from: Data(
                #"{"membership":{"id":"mem-1","user_id":"user-1","plan":"pro","status":"active","started_at":"2026-03-01T10:00:00Z","expires_at":null,"granted_by_id":null,"cancelled_at":null,"expired_at":null,"past_due_at":null,"paused_at":null,"cancel_at_period_end":false,"latest_change_id":null,"created_at":"2026-03-01T10:00:00Z","updated_at":"2026-03-01T10:00:00Z","sku":{"id":"sku-1","plan":"pro","price":{"amount":1299,"currency":"usd"},"interval":"monthly","stripe_price_id":"price-1","retired_at":null},"has_stripe_subscription":true}}"#
                    .utf8
            )
        )
        XCTAssertEqual(membership.membership?.hasStripeSubscription, true)
        XCTAssertEqual(membership.membership?.sku.price, try Money(amount: 1_299, currency: "usd"))

        let profileLink = try decoder.decode(
            VouchaModels.ProfileLink.self,
            from: Data(
                #"{"id":"link-1","user_id":"user-1","link_type":"github","sort_order":0,"url_id":null,"url":"https://example.com","handle":"octocat","name":"GitHub","image_id":null,"created_at":"2026-03-01T10:00:00Z","updated_at":"2026-03-01T10:00:00Z"}"#
                    .utf8
            )
        )
        XCTAssertEqual(profileLink.linkType, .github)
        XCTAssertEqual(profileLink.url, "https://example.com")

        let pushSubscription = try decoder.decode(
            WebPushSubscription.self,
            from: Data(
                #"{"id":"sub-1","user_id":"user-1","endpoint":"https://push.example.com","p256dh":"abcdabcdabcdabcd","auth":"abcdefgh","expiration_time_ms":null,"user_agent":"Safari","last_success_at":null,"last_failure_at":null,"created_at":"2026-03-01T10:00:00Z","updated_at":"2026-03-01T10:00:00Z"}"#
                    .utf8
            )
        )
        XCTAssertEqual(pushSubscription.userAgent, "Safari")
    }

    func testImageUploadModelsDecode() throws {
        let decoder = makeVouchaDecoder()

        let upload = try decoder.decode(
            ImageUploadResponse.self,
            from: Data(
                #"{"upload":{"image_id":"image-1","upload_url":"https://example.com/upload","content_type":"image/jpeg","expires_at":"2026-03-01T11:00:00Z"}}"#
                    .utf8
            )
        )
        XCTAssertEqual(upload.upload.imageId, "image-1")
        XCTAssertEqual(
            upload.upload.expiresAt,
            try? Date("2026-03-01T11:00:00Z", strategy: Date.ISO8601FormatStyle(includingFractionalSeconds: false))
        )

        let uploadWithNullExpiry = try decoder.decode(
            ImageUploadResponse.self,
            from: Data(
                #"{"upload":{"image_id":"image-2","upload_url":"https://example.com/upload","content_type":"image/jpeg","expires_at":null}}"#
                    .utf8
            )
        )
        XCTAssertNil(uploadWithNullExpiry.upload.expiresAt)

        let state = try decoder.decode(
            ImageUploadStateResponse.self,
            from: Data(
                #"{"upload_state":{"id":"image-1","upload_status":"processing","upload_error":null,"ready":false,"blocked":false}}"#
                    .utf8
            )
        )
        XCTAssertEqual(state.uploadState.uploadStatus, .processing)
    }
}
