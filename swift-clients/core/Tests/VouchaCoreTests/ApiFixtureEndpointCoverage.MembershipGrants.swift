@testable import VouchaAPI

let membershipGrantAndAncestorFixtureEndpoints: [String: Endpoint] = [
    "native.comments.ancestors.bounded.shallow": Endpoint.postAncestors(postId: "comment-b", limit: 5),
    "native.comments.ancestors.bounded.deep-initial": Endpoint.postAncestors(
        postId: "bounded-ancestor-comment-7",
        limit: 5
    ),
    "native.comments.ancestors.bounded.deep-continuation": Endpoint.postAncestors(
        postId: "bounded-ancestor-comment-7",
        after: "fixture-ancestor-deep-initial-end",
        limit: 5
    ),
    "native.memberships.grant.delete.default": Endpoint.revokeMembershipGrant(
        grantId: "00000000-0000-7000-8000-000000000802",
        reason: "Incorrect grant"
    )
]
