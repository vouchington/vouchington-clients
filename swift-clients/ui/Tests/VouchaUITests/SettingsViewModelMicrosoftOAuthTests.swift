import Foundation
import ViewInspector
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelOAuthTests {
    func testMicrosoftConnectionControlFollowsCapabilityAndPendingFlow() async throws {
        let fixture = try makeFixture()
        seedCapabilities()
        await fixture.viewModel.loadOAuthCapabilities()

        XCTAssertFalse(fixture.viewModel.canConnectOAuthAccount(provider: .microsoft))
        XCTAssertNoThrow(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Microsoft"))
        XCTAssertThrowsError(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Microsoft").parent().find(ViewType.Button.self))

        seedCapabilities(microsoftNative: true)
        await fixture.viewModel.loadOAuthCapabilities()
        XCTAssertTrue(fixture.viewModel.canConnectOAuthAccount(provider: .microsoft))
        XCTAssertFalse(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Microsoft").parent()
            .find(ViewType.Button.self).isDisabled())
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/microsoft/authorizations"] = (
            Data("""
            {"flow_id":"microsoft-flow",
            "redirect_url":"https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            "expires_at":"2027-01-01T00:05:00.000Z"}
            """.utf8),
            200
        )
        var openedURL: URL?

        await fixture.viewModel.beginOAuthConnection(provider: .microsoft) { openedURL = $0 }

        XCTAssertEqual(openedURL?.host, "login.microsoftonline.com")
        XCTAssertEqual(fixture.coordinator.pending?.provider, .microsoft)
        XCTAssertTrue(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Microsoft").parent()
            .find(ViewType.Button.self).isDisabled())
    }

    func testMicrosoftConnectedIdentityRendersAndDisconnects() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let fixture = try makeFixture()
        await fixture.viewModel.load()
        seedCapabilities(microsoftNative: true)
        await fixture.viewModel.loadOAuthCapabilities()

        XCTAssertEqual(fixture.viewModel.account(for: .microsoft)?.name, "Microsoft Alice")
        XCTAssertNoThrow(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Microsoft Alice"))
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/microsoft/connect"] = (
            Data("{}".utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )

        await fixture.viewModel.disconnectOAuthAccount(provider: .microsoft)

        XCTAssertNil(fixture.viewModel.account(for: .microsoft))
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last { $0 == "DELETE" }, "DELETE")
    }
}
