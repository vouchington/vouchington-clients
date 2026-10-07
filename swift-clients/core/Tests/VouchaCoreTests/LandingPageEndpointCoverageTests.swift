import Foundation
@testable import VouchaAPI
import XCTest

final class LandingPageEndpointCoverageTests: XCTestCase {
    func testLandingPageEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(Endpoint.myLandingPages, path: "/api/v1/my/landing-pages")
        assertEndpoint(Endpoint.myLandingPageCandidates, path: "/api/v1/my/landing-pages/candidates")
        assertEndpoint(Endpoint.myLandingPage(id: "page 1"), path: "/api/v1/my/landing-pages/page%201")
        assertEndpoint(
            Endpoint.createMyLandingPage(title: "My Links", subtitle: .value("Everything"), slug: "my-links"),
            method: .POST,
            path: "/api/v1/my/landing-pages",
            body: [
                "title": "My Links",
                "subtitle": "Everything",
                "slug": "my-links"
            ]
        )
        assertEndpoint(
            Endpoint.createMyLandingPage(title: "My Links", subtitle: .null, slug: "my-links"),
            method: .POST,
            path: "/api/v1/my/landing-pages",
            body: [
                "title": "My Links",
                "subtitle": NSNull(),
                "slug": "my-links"
            ]
        )
        assertEndpoint(
            Endpoint.updateMyLandingPage(
                id: "page 1",
                body: LandingPageMetadataBody(title: "Updated Links", subtitle: .null, slug: "updated-links")
            ),
            method: .PATCH,
            path: "/api/v1/my/landing-pages/page%201",
            body: [
                "title": "Updated Links",
                "subtitle": NSNull(),
                "slug": "updated-links"
            ]
        )
        assertEndpoint(
            Endpoint.setDefaultMyLandingPage(id: "page 1"),
            method: .PATCH,
            path: "/api/v1/my/landing-pages/page%201",
            body: [
                "is_default": true
            ]
        )
        assertEndpoint(
            Endpoint.deleteMyLandingPage(id: "page 1"),
            method: .DELETE,
            path: "/api/v1/my/landing-pages/page%201"
        )
    }

    func testLandingPageReplaceItemsEndpointEncodesEveryItemType() {
        assertEndpoint(
            Endpoint.replaceMyLandingPageItems(
                id: "page 1",
                items: [
                    .profileLink(id: "profile-link-1"),
                    .review(id: "review-1"),
                    .referralLink(id: "referral-link-1"),
                    .topicGroup(
                        topicId: "topic-1",
                        entries: [
                            .review(id: "review-2"),
                            .referralLink(id: "referral-link-2")
                        ]
                    ),
                    .link(label: "Newsletter", url: "https://example.com/newsletter")
                ]
            ),
            method: .PUT,
            path: "/api/v1/my/landing-pages/page%201/items",
            body: [
                "items": [
                    [
                        "type": "profile_link",
                        "profile_link_id": "profile-link-1"
                    ],
                    [
                        "type": "review",
                        "review_post_id": "review-1"
                    ],
                    [
                        "type": "referral_link",
                        "referral_link_id": "referral-link-1"
                    ],
                    [
                        "type": "topic_group",
                        "topic_id": "topic-1",
                        "entries": [
                            [
                                "type": "review",
                                "review_post_id": "review-2"
                            ],
                            [
                                "type": "referral_link",
                                "referral_link_id": "referral-link-2"
                            ]
                        ]
                    ],
                    [
                        "type": "link",
                        "label": "Newsletter",
                        "url": "https://example.com/newsletter"
                    ]
                ]
            ]
        )
    }
}
