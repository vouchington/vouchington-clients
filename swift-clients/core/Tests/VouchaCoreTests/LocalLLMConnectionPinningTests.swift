import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMConnectionPinningTests: XCTestCase {
    func testSelectValidatedAddressesRefusesACleartextRequestWhenNoResolvedAddressIsPrivate() {
        XCTAssertThrowsError(
            try LocalLLMConnectionPinning.selectValidatedAddresses(
                [ip("8.8.8.8")],
                requestURL: URL(string: "http://models.example.test/v1/responses"),
                host: "models.example.test"
            )
        ) { error in
            XCTAssertEqual(
                error as? LocalLLMError,
                .resolvedAddressNotPrivate(host: "models.example.test")
            )
        }
    }

    func testSelectValidatedAddressesAllowsACleartextRequestWhoseResolvedAddressIsPrivate() throws {
        let selected = try LocalLLMConnectionPinning.selectValidatedAddresses(
            [ip("192.168.1.20")],
            requestURL: URL(string: "http://models.example.test/v1/responses"),
            host: "models.example.test"
        )
        XCTAssertEqual(selected.map(\.textual), ["192.168.1.20"])
    }

    func testSelectValidatedAddressesFiltersOutPublicCandidatesButKeepsEveryPrivateOne() throws {
        let selected = try LocalLLMConnectionPinning.selectValidatedAddresses(
            [ip("8.8.8.8"), ip("10.0.0.5"), ip("192.168.1.1")],
            requestURL: URL(string: "http://models.example.test/v1/responses"),
            host: "models.example.test"
        )
        XCTAssertEqual(selected.map(\.textual), ["10.0.0.5", "192.168.1.1"])
    }

    func testSelectValidatedAddressesDoesNotReCheckHttpsRequests() throws {
        let selected = try LocalLLMConnectionPinning.selectValidatedAddresses(
            [ip("8.8.8.8")],
            requestURL: URL(string: "https://models.example.test/v1/responses"),
            host: "models.example.test"
        )
        XCTAssertEqual(selected.map(\.textual), ["8.8.8.8"])
    }

    func testSelectValidatedAddressesThrowsWhenNoAddressWasResolved() {
        XCTAssertThrowsError(
            try LocalLLMConnectionPinning.selectValidatedAddresses(
                [],
                requestURL: URL(string: "http://models.example.test/v1/responses"),
                host: "models.example.test"
            )
        ) { error in
            XCTAssertEqual(
                error as? LocalLLMError,
                .noUsableResolvedAddress(host: "models.example.test")
            )
        }
    }

    func testSelectValidatedAddressesFailsClosedWhenTheRequestURLIsNil() {
        XCTAssertThrowsError(
            try LocalLLMConnectionPinning.selectValidatedAddresses(
                [ip("192.168.1.20")],
                requestURL: nil,
                host: "models.example.test"
            )
        ) { error in
            XCTAssertEqual(
                error as? LocalLLMError,
                .noUsableResolvedAddress(host: "models.example.test")
            )
        }
    }

    func testSelectValidatedAddressesKeepsMappedLoopbackAndRejectsMappedPrivate() throws {
        let selected = try LocalLLMConnectionPinning.selectValidatedAddresses(
            [ip("::ffff:127.0.0.1")],
            requestURL: URL(string: "http://localhost/v1/responses"),
            host: "localhost"
        )
        XCTAssertEqual(selected.map(\.textual), ["::ffff:127.0.0.1"])
        XCTAssertThrowsError(
            try LocalLLMConnectionPinning.selectValidatedAddresses(
                [ip("::ffff:192.168.1.1")],
                requestURL: URL(string: "http://localhost/v1/responses"),
                host: "localhost"
            )
        ) { error in
            XCTAssertEqual(error as? LocalLLMError, .resolvedAddressNotPrivate(host: "localhost"))
        }
    }

    func testSelectValidatedAddressesRejectsCGNATUntilTheSharedPolicyLands() {
        XCTAssertThrowsError(
            try LocalLLMConnectionPinning.selectValidatedAddresses(
                [ip("100.64.0.1")],
                requestURL: URL(string: "http://models.example.test/v1/responses"),
                host: "models.example.test"
            )
        ) { error in
            XCTAssertEqual(
                error as? LocalLLMError,
                .resolvedAddressNotPrivate(host: "models.example.test")
            )
        }
    }

    func testPinnedRequestRewritesTheURLAndSetsANonDefaultPortHostHeader() throws {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "http://model-box.local:11434/v1/responses")))
        request.httpMethod = "POST"
        let pinned = try LocalLLMConnectionPinning.pinnedRequest(
            from: request,
            address: ip("192.168.1.20"),
            originalHost: "model-box.local"
        )
        XCTAssertEqual(pinned.url?.host, "192.168.1.20")
        XCTAssertEqual(pinned.url?.path, "/v1/responses")
        XCTAssertEqual(pinned.value(forHTTPHeaderField: "Host"), "model-box.local:11434")
    }

    func testPinnedRequestOmitsTheDefaultHTTPPortFromTheHostHeader() throws {
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://model-box.local/v1/responses")))
        let pinned = try LocalLLMConnectionPinning.pinnedRequest(
            from: request,
            address: ip("192.168.1.20"),
            originalHost: "model-box.local"
        )
        XCTAssertEqual(pinned.value(forHTTPHeaderField: "Host"), "model-box.local")
    }

    func testPinnedRequestBracketsIPv6AndMapsMappedLoopbackToIPv4() throws {
        let ipv6 = try LocalLLMConnectionPinning.pinnedRequest(
            from: URLRequest(url: XCTUnwrap(URL(string: "http://localhost:11434/v1/responses"))),
            address: ip("::1"),
            originalHost: "localhost"
        )
        XCTAssertTrue(try XCTUnwrap(ipv6.url?.absoluteString).contains("[::1]"))
        XCTAssertEqual(ipv6.url?.host, "::1")

        let mapped = try LocalLLMConnectionPinning.pinnedRequest(
            from: URLRequest(url: XCTUnwrap(URL(string: "http://localhost:11434/v1/responses"))),
            address: ip("::ffff:127.0.0.1"),
            originalHost: "localhost"
        )
        XCTAssertEqual(mapped.url?.host, "127.0.0.1")
        XCTAssertFalse(try XCTUnwrap(mapped.url?.absoluteString).contains("ffff"))
    }

    func testPinningErrorsDescribeTheHostWithoutLookingLikeARedirect() {
        XCTAssertTrue(
            LocalLLMError.resolvedAddressNotPrivate(host: "nas.local")
                .errorDescription?
                .contains("nas.local") == true
        )
        XCTAssertEqual(
            LocalLLMError.dnsResolutionFailed.errorDescription,
            "The local model endpoint could not be resolved."
        )
        XCTAssertTrue(
            LocalLLMError.noUsableResolvedAddress(host: "nas.local")
                .errorDescription?
                .contains("nas.local") == true
        )
        XCTAssertTrue(
            LocalLLMError.unableToConnect(host: "nas.local")
                .errorDescription?
                .contains("nas.local") == true
        )
        XCTAssertFalse(
            LocalLLMError.unableToConnect(host: "nas.local")
                .errorDescription?
                .localizedCaseInsensitiveContains("redirect") == true
        )
    }

    func testPinnedRequestDoesNotOverwriteAnExistingHostHeader() throws {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "http://127.0.0.1:11434/v1/responses")))
        request.setValue("127.0.0.1:11434", forHTTPHeaderField: "Host")
        let pinned = try LocalLLMConnectionPinning.pinnedRequest(
            from: request,
            address: ip("127.0.0.1"),
            originalHost: "127.0.0.1"
        )
        XCTAssertEqual(pinned.value(forHTTPHeaderField: "Host"), "127.0.0.1:11434")
    }

    private func ip(_ textual: String) -> LocalLLMIPAddress {
        guard let address = LocalLLMIPAddress.parse(textual) else {
            XCTFail("Expected a parseable address: \(textual)")
            return LocalLLMIPAddress(family: .ipv4, octets: [0, 0, 0, 0], textual: textual)
        }
        return address
    }
}
