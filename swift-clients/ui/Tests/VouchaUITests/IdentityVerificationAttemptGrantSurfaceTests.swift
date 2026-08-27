import SwiftUI
import ViewInspector
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class IdentityVerificationAttemptGrantSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testNonAdministratorShowsEmptyState() throws {
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: false
        )
        let surface = IdentityVerificationAttemptGrantSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        XCTAssertNoThrow(try surface.inspect().find(EmptyStateView.self))
        XCTAssertNoThrow(try surface.inspect().find(text: "Administrator required"))
        XCTAssertThrowsError(
            try surface.inspect().find(viewWithAccessibilityIdentifier: "identity-verification-grant-submit")
        )
    }

    func testAdministratorFormShowsLoadAndSubmissionMessages() throws {
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        viewModel.targetUserId = "user-1"
        viewModel.note = "Terminal Free attempt reviewed."
        viewModel.loadError = .message(.nativeSwiftIdentityVerificationLoadFailure)
        viewModel.submissionMessage = .message(.nativeSwiftIdentityVerificationSuccess)
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/user-1/identity-verification-attempts"] = (
            Data(#"{"granted":true}"#.utf8),
            201
        )
        let surface = IdentityVerificationAttemptGrantSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        XCTAssertNoThrow(try surface.inspect().find(text: "Unable to load this user."))
        XCTAssertNoThrow(try surface.inspect().find(text: "Identity-verification retry granted."))
        try surface.inspect().find(button: "Grant identity retry").tap()
    }

    func testSubmittingLabelReplacesGrantButtonTitle() throws {
        let viewModel = try IdentityVerificationGrantViewModel(
            client: makeClient(),
            idOrUsername: "alice",
            isAdministrator: true
        )
        viewModel.isSubmitting = true
        let surface = IdentityVerificationAttemptGrantSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        XCTAssertNoThrow(try surface.inspect().find(button: "Granting..."))
        XCTAssertThrowsError(try surface.inspect().find(button: "Grant identity retry"))
    }

    func testProfileAdminButtonNavigatesToUserAdmin() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        var navigated: String?
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            isAdministrator: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { navigated = $0 }
        )

        try surface.inspect().find(viewWithAccessibilityIdentifier: "user-profile-admin").button().tap()
        XCTAssertEqual(navigated, "/user/alice/admin")
    }
}
