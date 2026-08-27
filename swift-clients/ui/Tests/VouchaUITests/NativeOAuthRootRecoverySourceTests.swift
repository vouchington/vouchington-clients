import Foundation
import XCTest

final class NativeOAuthRootRecoverySourceTests: XCTestCase {
    func testOrdinaryRootLaunchResumesBeforeConsumingAuthorizationResult() throws {
        let rootSource = try repoSource("swift-clients/apps/shared-app/RootView.swift")
        let recoverySource = try repoSource("swift-clients/apps/shared-app/RootView+OAuth.swift")
        let iosAppSource = try repoSource("swift-clients/apps/iOS/Sources/VouchaApp.swift")
        let macOSAppSource = try repoSource("swift-clients/apps/macOS/Sources/VouchaApp.swift")
        let recover = "await restoreSessionAndNativeAuthorizationsOnLaunch()"
        let consume = "handleNativeOAuthAuthorizationResult(nativeOAuthCoordinator.result)"

        let recoverRange = try XCTUnwrap(rootSource.range(of: recover))
        let consumeRange = try XCTUnwrap(rootSource.range(of: consume))
        XCTAssertLessThan(recoverRange.lowerBound, consumeRange.lowerBound)

        let restoreRange = try XCTUnwrap(recoverySource.range(of: "restoreSession()"))
        let oauthRange = try XCTUnwrap(recoverySource.range(of: "resumePendingAuthorization()"))
        let blueskyRange = try XCTUnwrap(recoverySource.range(of: "resumePendingNativeBlueskyLink"))
        XCTAssertLessThan(restoreRange.lowerBound, oauthRange.lowerBound)
        XCTAssertLessThan(oauthRange.lowerBound, blueskyRange.lowerBound)
        XCTAssertTrue(recoverySource.contains("guard viewModelFactory.restoresSessionOnLaunch"))
        XCTAssertFalse(iosAppSource.contains(".task {"))
        XCTAssertFalse(macOSAppSource.contains(".task {"))
    }

    func testHandledCallbackRoutesPendingPurposeToRecoverySurface() throws {
        let source = try repoSource("swift-clients/apps/shared-app/RootView+Bluesky.swift")

        XCTAssertTrue(source.contains("switch oauthCoordinator.pending?.purpose"))
        XCTAssertTrue(source.contains("case .authenticate:"))
        XCTAssertTrue(source.contains("showingSignIn = true"))
        XCTAssertTrue(source.contains("case .connect:"))
        XCTAssertTrue(source.contains(#"routeNativeTargetPath("/my/identity")"#))
        XCTAssertTrue(source.contains("""
                    case nil:
                        showingSignIn = true
        """))
    }

    func testUITestingLaunchesUseIsolatedCredentialsAndNeverRestore() throws {
        let factorySource = try repoSource("swift-clients/apps/shared-app/ViewModelFactory.swift")
        let isolatedSource = try repoSource("swift-clients/apps/shared-app/ViewModelFactory+UITesting.swift")
        let launchSource = try repoSource("swift-clients/apps/shared-tests/LaunchSmokeTests.swift")
        let appAttestSource = try repoSource(
            "swift-clients/core/Sources/VouchaAuth/AppAttestationService.swift"
        )

        XCTAssertTrue(factorySource.contains(#"hasPrefix("--ui-testing-")"#))
        XCTAssertTrue(isolatedSource.contains("HTTPCookieStorage()"))
        XCTAssertTrue(isolatedSource.contains("bootstrapSession: false"))
        XCTAssertTrue(isolatedSource.contains("UITestingOAuthAuthorizationSecureState()"))
        XCTAssertTrue(isolatedSource.contains("UITestingFeatureFlagOverrideStore()"))
        XCTAssertTrue(isolatedSource.contains("restoresSessionOnLaunch: false"))
        XCTAssertFalse(isolatedSource.contains("KeychainCookieStorage"))
        XCTAssertFalse(isolatedSource.contains("AppAttestKeyStore"))
        XCTAssertTrue(launchSource.contains("--ui-testing-launch-smoke"))
        XCTAssertTrue(launchSource.contains("https://ui-testing.voucha.invalid"))
        XCTAssertTrue(launchSource.contains("VOUCHA_DISABLE_APP_ATTEST"))
        XCTAssertTrue(appAttestSource.contains(#"environment["VOUCHA_DISABLE_APP_ATTEST"] != "1""#))
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        var roots = [current, URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()]
        for _ in 0 ..< 16 {
            roots.append(roots[roots.count - 1].deletingLastPathComponent())
        }
        for root in roots {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
        }
        throw XCTSkip("Could not find \(relative)")
    }
}
