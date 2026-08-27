import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealsIdentityLoadingTests: NativeRouteSurfaceViewModelTestCase {
    func testSuccessfulIdentityWithoutSuspensionClearsCachedDate() throws {
        let cachedDate = Date(timeIntervalSince1970: 1)
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        viewModel.suspensionDate = cachedDate

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let pending = try decoder.decode(
            ModerationAppealListResponse.self,
            from: ModerationAppealsTestSupport.list([])
        )

        viewModel.apply(pending: pending)

        XCTAssertEqual(viewModel.suspensionDate, cachedDate)

        let identity = try decoder.decode(
            NativeIdentityResponse.self,
            from: PrivateUserTestFixture.identityEnvelope()
        )

        viewModel.apply(identity: identity)

        XCTAssertNil(viewModel.suspensionDate)
    }
}
