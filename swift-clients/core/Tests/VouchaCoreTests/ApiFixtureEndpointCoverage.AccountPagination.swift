@testable import VouchaAPI

extension ApiFixtureEndpointCoverageTests {
    static let accountPaginationFixtureEndpointRegistry: [String: Endpoint] = [
        "native.users.delete.default": Endpoint.deleteUser(idOrSlug: "user-abc"),
        "native.users.data-request.default": Endpoint.userDataRequest(idOrSlug: "user-abc"),
        "native.users.data-request.create.default": Endpoint.createUserDataRequest(idOrSlug: "user-abc"),
        "native.auth.sessions.default": Endpoint.authSessions(
            after: "fixture-owner-scoped-session-cursor",
            limit: 1
        ),
        "native.auth.bluesky.link.default": Endpoint.beginBlueskyAccountLink(handle: "alice.bsky.social"),
        "native.auth.bluesky.link.native": Endpoint(
            .POST,
            path: "/api/v1/auth/bluesky/link",
            body: [
                "callback_mode": "native",
                "completion_proof_challenge": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "handle": "alice.bsky.social"
            ]
        ),
        "native.auth.bluesky.link-completion.default": Endpoint(
            .POST,
            path: "/api/v1/auth/bluesky/link-completions",
            body: [
                "completion_proof_verifier": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "completion_token": "fixture-completion-token",
                "flow_id": "00000000-0000-7000-8000-00000000b501"
            ]
        ),
        "native.auth.bluesky.unlink.default": Endpoint.disconnectBlueskyAccount,
        "native.fediverse.instances.default": Endpoint.fediverseInstances(),
        "native.fediverse.instances.page-2": Endpoint.fediverseInstances(after: "next", sort: nil),
        "native.fediverse.instances.empty": Endpoint.fediverseInstances(
            limit: nil, query: "no-match-fixture-query", sort: nil
        ),
        "native.fediverse.instance.slug": Endpoint.fediverseInstance(idOrSlug: "social-example"),
        "native.fediverse.instance.uuid": Endpoint.fediverseInstance(
            idOrSlug: "00000000-0000-7000-8000-00000000f003"
        ),
        "swift.my.identity.default": Endpoint.myIdentity,
        "native.my.email-preferences.default": Endpoint.myEmailPreferences,
        "native.my.email-addresses.empty": Endpoint.myEmailAddresses(
            after: "fixture-owner-scoped-email-cursor",
            limit: 1
        ),
        "native.my.api-keys.paginated": Endpoint.myApiKeys(
            after: "fixture-owner-scoped-api-key-cursor",
            limit: 1
        ),
        "native.my.push-subscriptions.paginated": Endpoint.myPushSubscriptions(
            after: "fixture-owner-scoped-push-cursor",
            limit: 1
        ),
        "native.community.pending-reports.paginated": Endpoint.communityPendingReports(
            idOrSlug: "fixture-community",
            after: "fixture-community-role-and-sort-scoped-report-cursor",
            limit: 1,
            sort: "created_at_desc"
        ),
        "native.my.email-addresses.request.default": Endpoint.requestMyEmailAddressVerification(
            emailAddress: " Tests+Native-User@Voucha.ai "
        ),
        "native.my.email-addresses.verify.default": Endpoint.verifyMyEmailAddress(
            emailAddress: "tests+native-user@voucha.ai",
            token: "ABCD1234"
        ),
        "swift.my.profile.default": Endpoint.myProfile
    ]
}
