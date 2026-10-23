@testable import VouchaCore
import XCTest

final class SentryLaunchPlanTests: XCTestCase {
    func testMissingDsnDisablesDebugAndReleaseWithoutThrowing() {
        for configuration in [SentryBuildConfiguration.debug, .release] {
            let plan = SentryLaunchPlan.resolve(
                environment: ["VOUCHA_SENTRY_DSN": " \n"],
                packagedConfiguration: ["VOUCHA_SENTRY_DSN": ""],
                configuration: configuration,
                releaseName: "0.1.0+1",
                accountId: " account-1 "
            )
            XCTAssertFalse(plan.startsSDK)
            XCTAssertNil(plan.dsn)
            XCTAssertEqual(plan.tracesSampleRate, 0)
            XCTAssertFalse(plan.sendDefaultPii)
            XCTAssertEqual(plan.environmentName, configuration.environmentName)
            XCTAssertEqual(plan.releaseName, "0.1.0+1")
            XCTAssertEqual(plan.accountId, "account-1")
        }
    }

    func testEnvironmentDsnOverridesThePackagedValue() {
        let plan = SentryLaunchPlan.resolve(
            environment: ["VOUCHA_SENTRY_DSN": " https://public@example.invalid/1 "],
            packagedConfiguration: ["VOUCHA_SENTRY_DSN": "https://packaged@example.invalid/2"],
            configuration: .release,
            releaseName: "0.1.0+1",
            accountId: nil
        )
        XCTAssertTrue(plan.startsSDK)
        XCTAssertEqual(plan.dsn, "https://public@example.invalid/1")
        XCTAssertEqual(plan.environmentName, "release")
        XCTAssertEqual(plan.tracesSampleRate, SentryLaunchPlan.enabledTracesSampleRate)
        XCTAssertFalse(plan.sendDefaultPii)
        XCTAssertNil(plan.accountId)
    }

    func testAccountBindingSetsClearsAndSwitchesTheOpaqueId() {
        XCTAssertEqual(SentryAccountBinding.update(from: nil, to: " account-a "), .set("account-a"))
        XCTAssertEqual(SentryAccountBinding.update(from: "account-a", to: "account-a"), .unchanged)
        XCTAssertEqual(SentryAccountBinding.update(from: "account-a", to: "account-b"), .set("account-b"))
        XCTAssertEqual(SentryAccountBinding.update(from: "account-b", to: " "), .clear)
    }
}
