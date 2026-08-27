import Foundation
@testable import VouchaAPI
import XCTest

final class SettingsEndpointCoverageTests: XCTestCase {
    func testIdentityProfileAndPrivacyEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.myIdentity, path: "/api/v1/my/identity")
        assertEndpoint(
            Endpoint.updateMyIdentity(
                username: "alice",
                useDisplayNameFrom: .github,
                profileImageId: "image-1"
            ),
            method: .PATCH,
            path: "/api/v1/my/identity",
            body: [
                "username": "alice",
                "use_display_name_from": "github",
                "profile_image_id": "image-1"
            ]
        )
        assertEndpoint(
            Endpoint.updateMyIdentity(clearProfileImage: true),
            method: .PATCH,
            path: "/api/v1/my/identity",
            body: ["profile_image_id": NSNull()]
        )
        assertEndpoint(Endpoint.myProfile, path: "/api/v1/my/profile")
        assertEndpoint(Endpoint.myProfileLinks, path: "/api/v1/my/profile/links")
        assertEndpoint(
            Endpoint.createMyProfileLink(
                linkType: .github,
                handle: "octocat",
                name: "GitHub"
            ),
            method: .POST,
            path: "/api/v1/my/profile/links",
            body: [
                "link_type": "github",
                "handle": "octocat",
                "name": "GitHub"
            ]
        )
        assertEndpoint(
            Endpoint.reorderMyProfileLinks(ids: ["b", "a"]),
            method: .PUT,
            path: "/api/v1/my/profile/links/order",
            body: ["ids": ["b", "a"]]
        )
        assertEndpoint(
            Endpoint.updateMyProfileLink(id: "link 1", url: "https://example.com", imageId: nil),
            method: .PATCH,
            path: "/api/v1/my/profile/links/link%201",
            body: ["url": "https://example.com"]
        )
        assertEndpoint(
            Endpoint.deleteMyProfileLink(id: "link 1"),
            method: .DELETE,
            path: "/api/v1/my/profile/links/link%201"
        )
        assertEndpoint(
            Endpoint.updateUser(
                idOrSlug: "alice",
                followsVisibility: .followers,
                defaultPostBroadcast: "followers",
                defaultPostPrivacy: "private",
                engagementEmailsEnabled: false,
                newsDigestFrequency: "daily",
                moderationEmailsEnabled: true,
                communityDigestFrequency: "weekly",
                moderationEmailCadence: "selected_days",
                moderationEmailDaysOfWeek: [1, 3, 5],
                moderationEmailTimeOfDay: "09:00",
                moderationEmailTimezone: "America/Los_Angeles",
                thirdPartyMarketing: true,
                hnDiscussions: true,
                uiLocale: "fr"
            ),
            method: .PATCH,
            path: "/api/v1/users/alice",
            body: [
                "follows_visibility": "followers",
                "default_post_broadcast": "followers",
                "default_post_privacy": "private",
                "engagement_emails_enabled": false,
                "news_digest_frequency": "daily",
                "moderation_emails_enabled": true,
                "community_digest_frequency": "weekly",
                "moderation_email_cadence": "selected_days",
                "moderation_email_days_of_week": [1, 3, 5],
                "moderation_email_time_of_day": "09:00",
                "moderation_email_timezone": "America/Los_Angeles",
                "ui_locale": "fr",
                "third_party_marketing": true,
                "hn_discussions": true
            ]
        )
        assertEndpoint(
            Endpoint.updateUser(idOrSlug: "alice", clearUiLocale: true),
            method: .PATCH,
            path: "/api/v1/users/alice",
            body: ["ui_locale": NSNull()]
        )
        assertEndpoint(Endpoint.myEmailPreferences, path: "/api/v1/my/email-preferences")
        assertEndpoint(
            Endpoint.updateMyEmailPreferences(
                engagementEmailsEnabled: false,
                newsDigestFrequency: "daily",
                communityDigestFrequency: "none",
                moderationEmailCadence: "selected_days",
                moderationEmailDaysOfWeek: [1, 3, 5],
                moderationEmailTimeOfDay: "09:00",
                moderationEmailTimezone: "America/Los_Angeles"
            ),
            method: .PATCH,
            path: "/api/v1/my/email-preferences",
            body: [
                "engagement_emails_enabled": false,
                "news_digest_frequency": "daily",
                "community_digest_frequency": "none",
                "moderation_email_cadence": "selected_days",
                "moderation_email_days_of_week": [1, 3, 5],
                "moderation_email_time_of_day": "09:00",
                "moderation_email_timezone": "America/Los_Angeles"
            ]
        )
        assertEndpoint(Endpoint.deleteUser(idOrSlug: "alice"), method: .DELETE, path: "/api/v1/users/alice")
        assertEndpoint(Endpoint.userDataRequest(idOrSlug: "alice"), path: "/api/v1/users/alice/data-request")
        assertEndpoint(
            Endpoint.createUserDataRequest(idOrSlug: "alice"),
            method: .POST,
            path: "/api/v1/users/alice/data-request"
        )
    }

    func testApiKeysMembershipNotificationsAndImagesUseExpectedRoutes() {
        assertEndpoint(Endpoint.authSessions(), path: "/api/v1/auth/sessions")
        assertEndpoint(
            Endpoint.revokeAuthSession(id: "session 1"),
            method: .DELETE,
            path: "/api/v1/auth/sessions/session%201"
        )
        assertEndpoint(
            Endpoint.revokeAuthSessions,
            method: .POST,
            path: "/api/v1/auth/sessions/revocations"
        )
        assertEndpoint(Endpoint.myApiKeys(), path: "/api/v1/my/api-keys")
        assertEndpoint(
            Endpoint.createMyApiKey(label: "Reader", permissions: ["rss-feeds:read"]),
            method: .POST,
            path: "/api/v1/my/api-keys",
            body: [
                "label": "Reader",
                "type": "rss",
                "permissions": ["rss-feeds:read"]
            ]
        )
        assertEndpoint(
            Endpoint.revokeMyApiKey(id: "key 1"),
            method: .DELETE,
            path: "/api/v1/my/api-keys/key%201"
        )
        assertEndpoint(Endpoint.membershipPlans, path: "/api/v1/memberships/plans")
        assertEndpoint(Endpoint.membershipMe, path: "/api/v1/memberships/me")
        assertEndpoint(
            Endpoint.membershipCheckout(
                priceId: "price-1",
                successUrl: "/my/membership?success=1",
                cancelUrl: "/my/membership?cancel=1"
            ),
            method: .POST,
            path: "/api/v1/memberships/checkout",
            body: [
                "price_id": "price-1",
                "success_url": "/my/membership?success=1",
                "cancel_url": "/my/membership?cancel=1"
            ]
        )
        assertEndpoint(
            Endpoint.membershipPortal(returnUrl: "/my/membership"),
            method: .POST,
            path: "/api/v1/memberships/billing-portal-sessions",
            body: ["return_url": "/my/membership"]
        )
        assertEndpoint(Endpoint.membershipCancel, method: .DELETE, path: "/api/v1/my/membership")
        assertEndpoint(Endpoint.myPushSubscriptions(), path: "/api/v1/my/notifications/push-subscriptions")
        assertEndpoint(
            Endpoint.revokeMyPushSubscription(id: "sub 1"),
            method: .DELETE,
            path: "/api/v1/my/notifications/push-subscriptions/sub%201"
        )
        assertEndpoint(
            Endpoint.imageUploadURL(contentType: "image/jpeg", contentLength: 1_234),
            method: .POST,
            path: "/api/v1/images/upload-url",
            body: ["content_type": "image/jpeg", "content_length": 1_234]
        )
        assertEndpoint(
            Endpoint.completeImageUpload(id: "image 1"),
            method: .POST,
            path: "/api/v1/images/image%201/completions"
        )
        assertEndpoint(
            Endpoint.imageUploadState(id: "image 1"),
            method: .GET,
            path: "/api/v1/images/image%201/upload-state"
        )
    }
}
