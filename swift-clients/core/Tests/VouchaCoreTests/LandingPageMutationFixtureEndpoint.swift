@testable import VouchaAPI

func nativeLandingPageItemsMutationEndpoint() -> Endpoint {
    Endpoint.replaceMyLandingPageItems(
        id: "landing-page-1",
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
    )
}
