#if canImport(Security)
    import Foundation
    @testable import VouchaAuth
    import XCTest

    final class KeychainCookieExpirationTests: XCTestCase {
        func testMaximumAgeExpirationUsesStoredCreationTime() {
            let now = Date(timeIntervalSinceReferenceDate: 1_000)
            let created = now.addingTimeInterval(-120).timeIntervalSinceReferenceDate

            XCTAssertTrue(
                KeychainCookieExpiration.isExpired(
                    expiresDate: nil,
                    properties: [
                        .maximumAge: "0",
                        KeychainCookieExpiration.createdPropertyKey: created
                    ],
                    now: now
                )
            )
            XCTAssertTrue(
                KeychainCookieExpiration.isExpired(
                    expiresDate: nil,
                    properties: [
                        .maximumAge: NSNumber(value: 60),
                        KeychainCookieExpiration.createdPropertyKey: created
                    ],
                    now: now
                )
            )
            XCTAssertFalse(
                KeychainCookieExpiration.isExpired(
                    expiresDate: nil,
                    properties: [
                        .maximumAge: 180.0,
                        KeychainCookieExpiration.createdPropertyKey: String(created)
                    ],
                    now: now
                )
            )
            XCTAssertTrue(
                KeychainCookieExpiration.isExpired(
                    expiresDate: nil,
                    properties: [.maximumAge: 180.0],
                    now: now
                )
            )
            XCTAssertFalse(
                KeychainCookieExpiration.isExpired(
                    expiresDate: nil,
                    properties: [.maximumAge: "invalid"],
                    now: now
                )
            )
            XCTAssertTrue(
                KeychainCookieExpiration.isExpired(
                    expiresDate: now.addingTimeInterval(-1),
                    properties: nil,
                    now: now
                )
            )
        }

        func testMaximumAgeCreationTimeIsStampedBeforeStorage() throws {
            let now = Date(timeIntervalSinceReferenceDate: 1_000)
            let properties = try XCTUnwrap(
                KeychainCookieExpiration.propertiesByStampingCreatedDateIfNeeded(
                    [
                        .name: "dt",
                        .value: "tok123",
                        .domain: "api.voucha.ai",
                        .path: "/",
                        .maximumAge: "60"
                    ],
                    now: now
                )
            )

            XCTAssertEqual(
                properties[KeychainCookieExpiration.createdPropertyKey] as? TimeInterval,
                now.timeIntervalSinceReferenceDate
            )
            XCTAssertNil(
                KeychainCookieExpiration.propertiesByStampingCreatedDateIfNeeded(
                    properties,
                    now: now.addingTimeInterval(10)
                )
            )
        }

        func testMaximumAgeAbsoluteExpiryIsDerivedFromStoredCreationTime() throws {
            let created = Date(timeIntervalSinceReferenceDate: 1_000)
            let properties = KeychainCookieExpiration.propertiesByAddingAbsoluteExpiryIfNeeded(
                [
                    .name: "dt",
                    .value: "tok123",
                    .domain: "api.voucha.ai",
                    .path: "/",
                    .maximumAge: "60",
                    KeychainCookieExpiration.createdPropertyKey: created.timeIntervalSinceReferenceDate
                ]
            )

            XCTAssertEqual(
                try XCTUnwrap(properties[.expires] as? Date).timeIntervalSinceReferenceDate,
                created.addingTimeInterval(60).timeIntervalSinceReferenceDate
            )
            XCTAssertNil(properties[.maximumAge])

            let propertiesWithExistingExpires = KeychainCookieExpiration.propertiesByAddingAbsoluteExpiryIfNeeded(
                [
                    .name: "dt",
                    .value: "tok123",
                    .domain: "api.voucha.ai",
                    .path: "/",
                    .expires: created.addingTimeInterval(600),
                    .maximumAge: "60",
                    KeychainCookieExpiration.createdPropertyKey: created.timeIntervalSinceReferenceDate
                ]
            )

            XCTAssertEqual(
                try XCTUnwrap(propertiesWithExistingExpires[.expires] as? Date).timeIntervalSinceReferenceDate,
                created.addingTimeInterval(60).timeIntervalSinceReferenceDate
            )
            XCTAssertNil(propertiesWithExistingExpires[.maximumAge])
        }
    }
#endif
