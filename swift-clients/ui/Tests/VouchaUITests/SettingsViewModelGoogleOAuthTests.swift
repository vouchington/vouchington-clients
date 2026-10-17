import Foundation
import ViewInspector
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelOAuthTests {
    func seedCapabilities(linkedInNative: Bool = true, microsoftNative: Bool = false, googleNative: Bool = false) {
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            Data("""
            {
              "providers": ["facebook", "x", "github", "linkedin", "microsoft", "google"],
              "broker_capabilities": {
                "facebook": {"version": 1, "modes": {"web": true, "native": true}, "purposes": ["authenticate", "connect"]},
                "x": {"version": 1, "modes": {"web": true, "native": false}, "purposes": ["authenticate", "connect"]},
                "github": {"version": 1, "modes": {"web": true, "native": true}, "purposes": ["authenticate", "connect"]},
                "linkedin": {"version": 1, "modes": {"web": true, "native": \(
                    linkedInNative
                )}, "purposes": ["authenticate", "connect"]},
                "microsoft": {"version": 1, "modes": {"web": true, "native": \(
                    microsoftNative
                )}, "purposes": ["authenticate", "connect"]},
                "google": {"version": 1, "modes": {"web": true, "native": \(
                    googleNative
                )}, "purposes": ["authenticate", "connect"]}
              }
            }
            """.utf8),
            200
        )
    }

    func identityWithOAuthAccounts() -> Data {
        PrivateUserTestFixture.identityEnvelope(overrides: [
            "facebook_account": oauthAccount(id: "facebook-1", name: "Facebook Alice", email: nil),
            "x_account": oauthAccount(id: "x-1", name: "X Alice", email: nil),
            "github_account": oauthAccount(id: "github-1", name: "GitHub Alice", email: "alice@github.example"),
            "linkedin_account": oauthAccount(id: "linkedin-1", name: "LinkedIn Alice", email: nil),
            "microsoft_account": oauthAccount(id: "microsoft-1", name: "Microsoft Alice", email: nil),
            "google_account": oauthAccount(id: "google-1", name: "Google Alice", email: "alice@gmail.com")
        ])
    }

    func testGoogleCapabilityGatesConnectionAndGoogleIdentityRenders() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let fixture = try makeFixture()
        await fixture.viewModel.load()
        seedCapabilities(googleNative: true)

        await fixture.viewModel.loadOAuthCapabilities()

        XCTAssertTrue(fixture.viewModel.canConnectOAuthAccount(provider: .google))
        XCTAssertEqual(fixture.viewModel.account(for: .google)?.id, "google-1")
        XCTAssertEqual(fixture.viewModel.account(for: .google)?.emailAddress, "alice@gmail.com")
        XCTAssertNoThrow(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAccountsSection.inspect().find(text: "Google Alice"))

        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/google/authorizations"] = (
            Data("""
            {"flow_id":"google-flow","redirect_url":"https://accounts.google.com/o/oauth2/v2/auth",
            "expires_at":"2027-01-01T00:05:00.000Z"}
            """.utf8),
            200
        )
        var openedURL: URL?
        await fixture.viewModel.beginOAuthConnection(provider: .google) { openedURL = $0 }

        XCTAssertEqual(openedURL?.host, "accounts.google.com")
        XCTAssertEqual(fixture.coordinator.pending?.provider, .google)
        XCTAssertEqual(fixture.coordinator.pending?.purpose, .connect)
    }

    func oauthAccount(id: String, name: String, email: String?) -> [String: Any] {
        ["id": id, "name": name, "email_address": email.map { $0 as Any } ?? NSNull()]
    }
}
