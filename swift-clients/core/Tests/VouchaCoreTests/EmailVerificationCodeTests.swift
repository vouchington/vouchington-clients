import VouchaAPI
import VouchaCore
import XCTest

final class EmailVerificationCodeTests: XCTestCase {
    func testAllEmailEndpointsNormalizeASCIIBase32() throws {
        for endpoint in emailEndpoints(code: "abcz234567") {
            let body = try XCTUnwrap(endpoint.body)
            let data = try JSONEncoder().encode(body)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: String])
            XCTAssertEqual(json["token"] ?? json["code"], "ABCZ234567")
        }
    }

    func testAllEmailEndpointsRejectMalformedCodesBeforeEncodingARequest() throws {
        for code in [
            "",
            "123456",
            "ABCDEFGH2",
            "ABCDEFGH234",
            "ABCDEFGH01",
            "ABCDEFGH89",
            "ABCDEFGH2=",
            "ABCDEFGHI ",
            "ＡBCDEFGH23",
            "ßBCDEFGH23",
            "abcdefgh2\n"
        ] {
            XCTAssertNil(EmailVerificationCode.normalized(code), code)
            for endpoint in emailEndpoints(code: code) {
                let body = try XCTUnwrap(endpoint.body)
                XCTAssertThrowsError(try JSONEncoder().encode(body), code)
            }
        }
    }

    func testTOTPRemainsSixNumericDigitsWithoutEmailNormalization() throws {
        for endpoint in [
            Endpoint.verifyMFATOTP(loginAttemptId: "attempt", code: "123456"),
            Endpoint.verifyReAuthTOTP("123456")
        ] {
            let body = try XCTUnwrap(endpoint.body)
            let json = try XCTUnwrap(JSONSerialization
                .jsonObject(with: JSONEncoder().encode(body)) as? [String: String])
            XCTAssertEqual(json["code"], "123456")
        }
    }

    private func emailEndpoints(code: String) -> [Endpoint] {
        [
            .verifyEmailOTP(email: "user@example.com", code: code),
            .verifyReAuthEmailCode(code),
            .verifyMyEmailAddress(emailAddress: "user@example.com", token: code)
        ]
    }
}
