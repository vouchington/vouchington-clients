import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTurnstileFallbackCallSiteTests: NativeRouteSurfaceViewModelTestCase {
    func testEmailChallengeUsesAuthServiceOriginAndCapturesTokenBeforeDismissal() throws {
        let client = try makeClient()
        let service = SignInService(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
        let model = EmailOTPViewModel(signInService: service)
        var presented = true
        let challenge = emailOTPTurnstileChallenge(
            siteKey: "email-key",
            isPresented: Binding(get: { presented }, set: { presented = $0 }),
            viewModel: model
        )

        assertChallenge(challenge, key: "email-key", action: .authEmailAddressTokens, origin: client.baseURL)
        challenge.onToken("email-token")
        XCTAssertEqual(model.turnstileToken, "email-token")
        XCTAssertFalse(presented)
    }

    func testCommunityChallengeUsesCreateActionAndStoresToken() throws {
        let client = try makeClient()
        let model = CommunityCreateViewModel(client: client)
        let surface = CommunityCreateSurface(viewModel: model, client: client, turnstileSiteKey: "community-key")
        let challenge = surface.turnstileChallenge

        assertChallenge(challenge, key: "community-key", action: .communitiesCreate, origin: client.baseURL)
        challenge.onToken("community-token")
        XCTAssertEqual(model.turnstileToken, "community-token")

        let noClient = CommunityCreateSurface(viewModel: model, turnstileSiteKey: "standalone-key")
        assertChallenge(
            noClient.turnstileChallenge,
            key: "standalone-key",
            action: .communitiesCreate,
            origin: AppConfig.shared.baseURL
        )
    }

    func testPostChallengeSelectsCommunityActionOnlyForCommunityPostsAndStoresToken() throws {
        let client = try makeClient()
        for community in [nil, "community-a"] as [String?] {
            let model = NativePostComposeViewModel(client: client, communityIdOrSlug: community)
            let surface = NativePostComposeSurface(
                client: client,
                communityIdOrSlug: community,
                turnstileSiteKey: "post-key"
            )
            let challenge = surface.turnstileChallenge(viewModel: model)

            assertChallenge(
                challenge,
                key: "post-key",
                action: community == nil ? .postsCreate : .communitiesCreatePost,
                origin: client.baseURL
            )
            challenge.onToken("post-token")
            XCTAssertEqual(model.turnstileToken, "post-token")
        }
        let model = NativePostComposeViewModel(client: nil)
        let standalone = NativePostComposeSurface(client: nil, turnstileSiteKey: "standalone-key")
        assertChallenge(
            standalone.turnstileChallenge(viewModel: model),
            key: "standalone-key",
            action: .postsCreate,
            origin: AppConfig.shared.baseURL
        )
    }

    func testCommentChallengeUsesReportActionForReportsAndPostsActionForReplies() async throws {
        let client = try makeClient()
        let cases: [(NativeCommentThreadComposerState.Kind, AppAttestActionTag)] = [
            (.reply(parentId: "parent"), .postsCreate),
            (.report(postId: "reported"), .reportsCreate)
        ]
        for (kind, action) in cases {
            var composer = NativeCommentThreadComposerState(kind: kind, markdown: "Reply")
            var captured: String?
            var submitted: NativeCommentThreadComposerState?
            let surface = NativeCommentThreadComposerSheet(
                composer: composer, showingTurnstile: .constant(false),
                client: client, turnstileSiteKey: "comment-key",
                onSubmit: { submitted = $0 }, onCancel: {}, onCaptureTurnstile: { captured = $0 }
            )
            let challenge = surface.turnstileChallenge(
                composer: Binding(get: { composer }, set: { composer = $0 })
            )
            assertChallenge(challenge, key: "comment-key", action: action, origin: client.baseURL)
            challenge.onToken("comment-token")
            XCTAssertEqual(captured, "comment-token")
            XCTAssertEqual(composer.turnstileToken, "comment-token")
            await surface.onSubmit(composer)
            XCTAssertEqual(submitted?.turnstileToken, "comment-token")
        }
    }

    func testDetailReportChallengeUsesClientOriginAndDismissesOnToken() throws {
        let client = try makeClient()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let model = NativeRouteSurfaceViewModel(entry: route.entry, client: client, routeMatch: route.match)
        model.detailShowingReportTurnstile = true
        let surface = NativeDetailSurface(entry: route.entry, viewModel: model, turnstileSiteKey: "report-key")
        let challenge = surface.turnstileChallenge

        assertChallenge(challenge, key: "report-key", action: .reportsCreate, origin: client.baseURL)
        challenge.onToken("report-token")
        XCTAssertFalse(model.detailShowingReportTurnstile)

        let noClient = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        let standalone = NativeDetailSurface(
            entry: route.entry,
            viewModel: noClient,
            turnstileSiteKey: "standalone-key"
        )
        assertChallenge(
            standalone.turnstileChallenge,
            key: "standalone-key",
            action: .reportsCreate,
            origin: AppConfig.shared.baseURL
        )
    }

    #if DEBUG
        func testMissingSiteKeysUseConfiguredDebugFallbackAcrossCallSites() throws {
            let client = try makeClient()
            let service = SignInService(
                client: client,
                sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
            )
            let email = emailOTPTurnstileChallenge(
                siteKey: nil,
                isPresented: .constant(true),
                viewModel: EmailOTPViewModel(signInService: service)
            )
            let community = CommunityCreateSurface(viewModel: .init(client: nil), turnstileSiteKey: nil)
            let post = NativePostComposeSurface(client: nil)
            let composer = NativeCommentThreadComposerSheet(
                composer: .init(kind: .reply(parentId: "parent"), markdown: "Reply"),
                showingTurnstile: .constant(false), turnstileSiteKey: nil,
                onSubmit: { _ in }, onCancel: {}, onCaptureTurnstile: { _ in }
            )
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
            let model = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
            let detail = NativeDetailSurface(entry: route.entry, viewModel: model)
            let challenges = [
                email,
                community.turnstileChallenge,
                post.turnstileChallenge(viewModel: .init(client: nil)),
                composer.turnstileChallenge(composer: .constant(composer.composer)),
                detail.turnstileChallenge
            ]
            XCTAssertTrue(challenges.allSatisfy { $0.siteKey == AppConfig.shared.requiredTurnstileSiteKey })
            XCTAssertEqual(
                composer.turnstileChallenge(composer: .constant(composer.composer)).baseURL,
                AppConfig.shared.baseURL
            )
        }
    #endif

    private func assertChallenge(
        _ challenge: NativeTurnstileChallengeView,
        key: String,
        action: AppAttestActionTag,
        origin: URL,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertEqual(challenge.siteKey, key, file: file, line: line)
        XCTAssertEqual(challenge.action, action, file: file, line: line)
        XCTAssertEqual(challenge.baseURL, origin, file: file, line: line)
    }
}
