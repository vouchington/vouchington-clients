import Foundation
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

@MainActor
extension NativeOAuthAuthorizationCoordinatorTests {
    func testGoogleConnectFinalizationRequiresGoogleIdentityAndConfirmsAccount() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "google-connect",
            provider: .google,
            purpose: .connect,
            now: fixture.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/google-connect/complete"
        ] = [(
            Data(#"{"oauth_account":{"id":"google-1","name":"Alice Google","email_address":"alice@gmail.com"}}"#.utf8),
            200
        )]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [
            (googleIdentityData(), 200)
        ]

        await fixture.coordinator.finalize(pending)

        XCTAssertEqual(fixture.coordinator.result, .connected(provider: .google))
        XCTAssertTrue(fixture.sessionManager.isSignedIn)
    }

    private func googleIdentityData() -> Data {
        Data("""
        {"identity":{
          "__entity_type":"user","id":"user-1","roles":["member"],"account_type":null,
          "google_account":{"id":"google-1","name":"Alice Google","email_address":"alice@gmail.com"},
          "cards_visibility":"everyone","rewards_program_statuses_visibility":"everyone",
          "spending_categories_visibility":"everyone","follows_visibility":"everyone",
          "topic_follows_visibility":"everyone","rss_feed_follows_visibility":"everyone",
          "community_memberships_visibility":"everyone","followers_visibility":"everyone",
          "likes_visibility":"everyone","direct_messages_audience":"users",
          "default_post_broadcast":"everyone","default_post_privacy":"public",
          "is_engagement_emails_enabled":true,"news_digest_frequency":"weekly",
          "is_moderation_emails_enabled":true,"community_digest_frequency":"weekly",
          "moderation_email_cadence":"daily","moderation_email_days_of_week":[1,2,3,4,5],
          "moderation_email_time_of_day":"09:00"
        }}
        """.utf8)
    }
}
