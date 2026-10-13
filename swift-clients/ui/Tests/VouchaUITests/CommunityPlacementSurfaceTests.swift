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
    func testWorkspaceAndCommunityImagesUseBothRevisionBoundPlacements() throws {
        let response = try APIClient.makeDecoder().decode(
            CommunityResponse.self,
            from: ApiFixtureLoader.data("web.communities.show.default")
        )
        let viewModel = CommunityDetailViewModel(client: nil, slug: response.community.slug)
        viewModel.communityDetail = response
        let surface = CommunityWorkspaceSurface(viewModel: viewModel)
        let images = try surface.inspect().findAll(ViewType.View<AsyncImageView>.self)

        XCTAssertEqual(images.count, 2)
        XCTAssertEqual(
            try images[0].actualView().resolvedURLString,
            "https://images.voucha.ai/images/placements/00000000-0000-7000-8000-000000000305/5/00000000-0000-7000-8000-000000000204?w=960"
        )
        XCTAssertEqual(
            try images[1].actualView().resolvedURLString,
            "https://images.voucha.ai/images/placements/00000000-0000-7000-8000-000000000304/2/00000000-0000-7000-8000-000000000203?w=96"
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
