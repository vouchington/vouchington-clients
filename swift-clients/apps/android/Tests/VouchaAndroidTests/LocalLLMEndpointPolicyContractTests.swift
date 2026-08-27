import Foundation
import XCTest

final class LocalLLMEndpointPolicyContractTests: XCTestCase {
    func testHostPolicyRowRejectsUnknownVerdict() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-android"],"endpoint":"http://192.168.1.20:11434","verdict":"rejetced","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }

    func testHostPolicyRowRejectsUnknownConsumer() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-croe"],"endpoint":"http://192.168.1.20:11434","verdict":"allowed","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }
}
