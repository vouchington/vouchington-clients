import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CommunityPlacementSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testWorkspaceImagesUseActiveClientImageHostForBothPlacements() throws {
        let response = try APIClient.makeDecoder().decode(
            CommunityResponse.self,
            from: ApiFixtureLoader.data("web.communities.show.default")
        )
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "https://api.self-hosted.example")),
            imageBaseURL: XCTUnwrap(URL(string: "https://cdn.self-hosted.example"))
        )
        let client = APIClient(config: config, cookieStorage: HTTPCookieStorage(), bootstrapSession: false)
        let viewModel = CommunityDetailViewModel(client: client, slug: response.community.slug)
        viewModel.communityDetail = response
        let surface = CommunityWorkspaceSurface(viewModel: viewModel)
        let images = try surface.inspect().findAll(ViewType.View<AsyncImageView>.self)

        XCTAssertEqual(images.count, 2)
        XCTAssertEqual(
            try images[0].actualView().resolvedURLString,
            "https://cdn.self-hosted.example/images/placements/00000000-0000-7000-8000-000000000305/5/00000000-0000-7000-8000-000000000204?w=960"
        )
        XCTAssertEqual(
            try images[1].actualView().resolvedURLString,
            "https://cdn.self-hosted.example/images/placements/00000000-0000-7000-8000-000000000304/2/00000000-0000-7000-8000-000000000203?w=96"
        )
    }

    func testCommunityBrowseAvatarUsesActiveClientImageHost() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (
            ApiFixtureLoader.data("entity-provenance.communities"),
            200
        )
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://browse-cdn.self-hosted.example"))
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let viewModel = CommunityBrowseViewModel(client: client)
        await viewModel.load()

        let avatars = try CommunityBrowseSurface(viewModel: viewModel)
            .inspect()
            .findAll(ViewType.View<CommunityPlacementAvatar>.self)
        XCTAssertEqual(avatars.count, 3)
        for avatar in avatars {
            XCTAssertEqual(try avatar.actualView().imageBaseURL, config.imageBaseURL)
        }

        let detail = try APIClient.makeDecoder().decode(
            CommunityResponse.self,
            from: ApiFixtureLoader.data("web.communities.show.default")
        )
        let placement = try XCTUnwrap(detail.community.profileImagePlacement)
        let configuredAvatar = CommunityPlacementAvatar(placement: placement, imageConfig: viewModel.imageConfig)
        XCTAssertEqual(
            configuredAvatar.url,
            "https://browse-cdn.self-hosted.example/images/placements/00000000-0000-7000-8000-000000000304/2/00000000-0000-7000-8000-000000000203?w=96"
        )
    }

    func testMissingProfilePlacementShowsOnlyAccessibleCommunityContextFallback() throws {
        let avatar = CommunityPlacementAvatar(placement: nil)
        let inspection = try avatar.inspect()

        XCTAssertTrue(inspection.findAll(ViewType.View<AsyncImageView>.self).isEmpty)
        XCTAssertNoThrow(try inspection.find(ViewType.Image.self))
    }

    func testCommunityAvatarUsesPlacementDescriptor() throws {
        let response = try APIClient.makeDecoder().decode(
            CommunityResponse.self,
            from: ApiFixtureLoader.data("web.communities.show.default")
        )
        let placement = try XCTUnwrap(response.community.profileImagePlacement)
        let avatar = try CommunityPlacementAvatar(placement: placement)
            .inspect()
            .find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(
            try avatar.actualView().resolvedURLString,
            "https://images.voucha.ai/images/placements/00000000-0000-7000-8000-000000000304/2/00000000-0000-7000-8000-000000000203?w=96"
        )
    }

}
