@testable import VouchaAPI

let topicClaimFixtureEndpoints: [String: Endpoint] = [
    "native.my-topic-claims.page": Endpoint.myTopicClaims(limit: 1),
    "native.topic-claims.page": Endpoint.topicClaims(
        idOrSlug: "00000000-0000-7000-8000-000000000301",
        mineOnly: true,
        activeOnly: true,
        limit: 1
    )
]
