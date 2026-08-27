import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class IdentityVerificationGrantViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testBlankNoteDoesNotPost() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixtureProfile, 200)
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        await viewModel.load()
        viewModel.note = "   "

        await viewModel.grant()

        XCTAssertFalse(viewModel.canSubmit)
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftIdentityVerificationValidation))
        XCTAssertFalse(
            CannedFeedURLProtocol.capturedURLs.contains { $0.path.contains("identity-verification-attempts") }
        )
    }

    func testGrantSuccessClearsNote() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixtureProfile, 200)
        let grantedUserId = try fixtureUserId()
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/\(grantedUserId)/identity-verification-attempts"] = (
            Data(#"{"granted":true}"#.utf8),
            201
        )
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        await viewModel.load()
        viewModel.note = "Terminal Free attempt reviewed."

        await viewModel.grant()

        XCTAssertEqual(viewModel.note, "")
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftIdentityVerificationSuccess))
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "POST")
    }

    func testGrantShows409AsUserContent() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixtureProfile, 200)
        let conflictUserId = try fixtureUserId()
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/\(conflictUserId)/identity-verification-attempts"] = (
            Data(
                #"{"message":"Support retries require a completed terminal Free identity verification attempt."}"#
                    .utf8
            ),
            409
        )
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        await viewModel.load()
        viewModel.note = "Please retry."

        await viewModel.grant()

        XCTAssertEqual(
            viewModel.submissionMessage,
            .userContent("Support retries require a completed terminal Free identity verification attempt.")
        )
        XCTAssertEqual(viewModel.note, "Please retry.")
    }

    func testLoadFailureClearsTargetUser() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (Data("{}".utf8), 500)
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertNil(viewModel.targetUserId)
        XCTAssertEqual(viewModel.loadError, .message(.nativeSwiftIdentityVerificationLoadFailure))
    }

    func testUngrantedResponseShowsFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixtureProfile, 200)
        let ungrantedUserId = try fixtureUserId()
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/\(ungrantedUserId)/identity-verification-attempts"] = (
            Data(#"{"granted":false}"#.utf8),
            201
        )
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        await viewModel.load()
        viewModel.note = "Terminal Free attempt reviewed."

        await viewModel.grant()

        XCTAssertEqual(viewModel.note, "Terminal Free attempt reviewed.")
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftIdentityVerificationFailure))
        XCTAssertFalse(viewModel.didGrant)
    }

    func testNonClientErrorShowsGenericFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (fixtureProfile, 200)
        let failedUserId = try fixtureUserId()
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/\(failedUserId)/identity-verification-attempts"] = (
            Data(#"{"message":"unavailable"}"#.utf8),
            500
        )
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        await viewModel.load()
        viewModel.note = "Please retry."

        await viewModel.grant()

        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftIdentityVerificationFailure))
    }

    func testNonAdministratorDoesNotLoadOrSubmit() async throws {
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: false
        )

        await viewModel.load()
        await viewModel.grant()

        XCTAssertNil(viewModel.targetUserId)
        XCTAssertFalse(viewModel.canSubmit)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    private var fixtureProfile: Data {
        ApiFixtureLoader.data("native.users.profile.default")
    }

    private func fixtureUserId() throws -> String {
        let payload = try JSONSerialization.jsonObject(with: fixtureProfile) as? [String: Any]
        let user = payload?["user"] as? [String: Any]
        return try XCTUnwrap(user?["id"] as? String)
    }
}
