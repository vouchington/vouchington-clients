import Foundation
@testable import VouchaAPI
import XCTest

final class PublicKeyPinningPolicyTests: XCTestCase {
    func testDefaultPolicyMarksVouchaApiHostsEligibleWithoutEnforcingPins() throws {
        let policy = PublicKeyPinningPolicy.vouchaDefault

        XCTAssertTrue(policy.isEligibleHost("voucha.ai"))
        XCTAssertTrue(policy.isEligibleHost("staging.voucha.ai"))
        XCTAssertFalse(policy.hasConfiguredPins)
        XCTAssertFalse(try policy.requiresPinning(for: XCTUnwrap(URL(string: "https://voucha.ai/api/v1/session"))))
    }

    func testPolicyAcceptsMatchingBackupPin() {
        let policy = PublicKeyPinningPolicy(
            eligibleHosts: ["voucha.ai"],
            pins: [
                PublicKeyPin(host: "voucha.ai", spkiSha256Base64: "current", label: "current"),
                PublicKeyPin(host: "voucha.ai", spkiSha256Base64: "next", label: "next")
            ]
        )

        XCTAssertTrue(policy.hasConfiguredPins)
        XCTAssertTrue(try policy.requiresPinning(for: XCTUnwrap(URL(string: "https://voucha.ai/api/v1/session"))))
        XCTAssertEqual(policy.validate(host: "voucha.ai", spkiSha256Base64: "next"), .accepted)
    }

    func testPolicyRejectsMismatchForPinnedHost() {
        let policy = PublicKeyPinningPolicy(
            eligibleHosts: ["voucha.ai"],
            pins: [PublicKeyPin(host: "voucha.ai", spkiSha256Base64: "expected", label: "current")]
        )

        XCTAssertEqual(policy.validate(host: "voucha.ai", spkiSha256Base64: "other"), .rejected)
    }

    func testPolicySkipsLocalhostAndCustomHosts() throws {
        let policy = PublicKeyPinningPolicy(
            eligibleHosts: ["voucha.ai"],
            pins: [PublicKeyPin(host: "voucha.ai", spkiSha256Base64: "expected", label: "current")]
        )

        XCTAssertFalse(try policy.requiresPinning(for: XCTUnwrap(URL(string: "http://localhost:2999"))))
        XCTAssertFalse(try policy.requiresPinning(for: XCTUnwrap(URL(string: "https://api.test"))))
        XCTAssertEqual(policy.validate(host: "api.test", spkiSha256Base64: "expected"), .notPinned)
    }

    func testPolicyNormalizesHostsCaseInsensitivelyAndTrimsWhitespace() {
        let policy = PublicKeyPinningPolicy(
            eligibleHosts: [" Voucha.AI "],
            pins: [PublicKeyPin(host: " VOUCHA.AI ", spkiSha256Base64: "expected", label: "current")]
        )

        XCTAssertTrue(policy.isEligibleHost("voucha.ai"))
        XCTAssertTrue(policy.isEligibleHost(" voucha.ai "))
        XCTAssertEqual(policy.validate(host: "voucha.ai", spkiSha256Base64: "expected"), .accepted)
        XCTAssertEqual(policy.validate(host: " voucha.ai ", spkiSha256Base64: "expected"), .accepted)
    }

    func testAPIClientCodingHelpersEncodeAndDecodeDates() throws {
        struct Payload: Codable, Equatable {
            let createdAt: Date
        }

        let fractionalData = Data(#"{"created_at":"2026-07-08T05:00:00.123Z"}"#.utf8)
        let wholeSecondData = Data(#"{"created_at":"2026-07-08T05:00:00Z"}"#.utf8)

        let fractional = try APIClient.makeDecoder().decode(Payload.self, from: fractionalData)
        let wholeSecond = try APIClient.makeDecoder().decode(Payload.self, from: wholeSecondData)
        let encoded = try APIClient.makeEncoder().encode(wholeSecond)

        XCTAssertEqual(fractional.createdAt.timeIntervalSince1970, 1_783_486_800.123, accuracy: 0.001)
        XCTAssertEqual(wholeSecond.createdAt.timeIntervalSince1970, 1_783_486_800, accuracy: 0.001)
        XCTAssertTrue(String(decoding: encoded, as: UTF8.self).contains("created_at"))
    }

    func testAPIClientDecoderRejectsInvalidDates() {
        struct Payload: Decodable {
            let createdAt: Date
        }

        XCTAssertThrowsError(
            try APIClient.makeDecoder().decode(Payload.self, from: Data(#"{"created_at":"not-a-date"}"#.utf8))
        )
    }

    func testAPIClientSessionHelperSkipsUnconfiguredPins() throws {
        let session = try APIClient.makeSession(
            configuration: .default,
            baseURL: XCTUnwrap(URL(string: "https://voucha.ai")),
            pinningPolicy: .vouchaDefault
        )

        XCTAssertNotNil(session)
    }

    #if canImport(Security)
        func testAPIClientSessionHelperBuildsPinnedSessionWhenPinsExist() throws {
            let policy = PublicKeyPinningPolicy(
                eligibleHosts: ["voucha.ai"],
                pins: [PublicKeyPin(host: "voucha.ai", spkiSha256Base64: "pin", label: "current")]
            )
            let session = try APIClient.makeSession(
                configuration: .default,
                baseURL: XCTUnwrap(URL(string: "https://voucha.ai")),
                pinningPolicy: policy
            )

            XCTAssertNotNil(session)
        }

        func testSPKIHeadersCoverSupportedAlgorithms() throws {
            let rsa2048 = try XCTUnwrap(
                PublicKeyPinningURLSessionDelegate.spkiHeader(keyType: kSecAttrKeyTypeRSA as String, keySize: 2_048)
            )
            let rsa3072 = try XCTUnwrap(
                PublicKeyPinningURLSessionDelegate.spkiHeader(keyType: kSecAttrKeyTypeRSA as String, keySize: 3_072)
            )
            let rsa4096 = try XCTUnwrap(
                PublicKeyPinningURLSessionDelegate.spkiHeader(keyType: kSecAttrKeyTypeRSA as String, keySize: 4_096)
            )
            let ec256 = try XCTUnwrap(
                PublicKeyPinningURLSessionDelegate.spkiHeader(
                    keyType: kSecAttrKeyTypeECSECPrimeRandom as String,
                    keySize: 256
                )
            )
            let ec384 = try XCTUnwrap(
                PublicKeyPinningURLSessionDelegate.spkiHeader(
                    keyType: kSecAttrKeyTypeECSECPrimeRandom as String,
                    keySize: 384
                )
            )

            XCTAssertEqual(rsa2048.count, 24)
            XCTAssertEqual(rsa3072.count, 24)
            XCTAssertEqual(rsa4096.count, 24)
            XCTAssertEqual(ec256.count, 26)
            XCTAssertEqual(ec384.count, 23)
            XCTAssertNil(PublicKeyPinningURLSessionDelegate.spkiHeader(
                keyType: kSecAttrKeyTypeRSA as String,
                keySize: 1_024
            ))
        }
    #endif
}
