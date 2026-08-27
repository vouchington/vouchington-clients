@testable import VouchaAPI

let userProfileFixtureEndpointRegistry: [String: Endpoint] = [
    "native.users.profile.default": Endpoint.user(idOrSlug: "alice", includeBio: true),
    "native.users.profile.restricted": Endpoint.user(idOrSlug: "restricted", includeBio: true),
    "native.users.vouch-context.default": Endpoint.userTrustContext(userId: "user-abc"),
    "native.users.profile.posts.all": Endpoint.userPosts(
        userId: "user-abc",
        postTypes: "review,discussion,comment"
    ),
    "native.users.profile.posts.reviews": Endpoint.userPosts(userId: "user-abc", postTypes: "review"),
    "native.users.profile.posts.discussions": Endpoint.userPosts(userId: "user-abc", postTypes: "discussion"),
    "native.users.profile.posts.comments": Endpoint.userPosts(userId: "user-abc", postTypes: "comment"),
    "native.users.profile.topics-following.first-page": Endpoint.userTopicsFollowing(userId: "user-abc"),
    "native.users.profile.topics-following.next-page": Endpoint.userTopicsFollowing(
        userId: "user-abc",
        after: "eyJ0aW1lc3RhbXAiOjE3ODI5MjE2MDAwMDAwMDAsImlkIjoiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMDAxIn0"
    ),
    "native.users.profile.sources-following.article": Endpoint.userRssFeeds(
        userId: "user-abc",
        feedType: "article",
        limit: 25
    ),
    "native.users.profile.communities-member.first-page": Endpoint.userCommunitiesMember(userId: "user-abc"),
    "native.users.profile.communities-member.next-page": Endpoint.userCommunitiesMember(
        userId: "user-abc",
        after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9"
    )
]
