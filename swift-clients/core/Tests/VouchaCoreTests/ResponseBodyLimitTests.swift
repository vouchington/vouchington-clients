import Foundation
@testable import VouchaAPI
import XCTest

final class ResponseBodyLimitTests: XCTestCase {
    func testDiagnosticBodyCollectorRejectsBytesPastItsCap() async {
        let (bytes, continuation) = AsyncThrowingStream<UInt8, Error>.makeStream()
        for _ in 0 ... ResponseBodyLimit.maximumDiagnosticBytes {
            continuation.yield(0x61)
        }
        continuation.finish()

        do {
            _ = try await ResponseBodyLimit.collect(bytes)
            XCTFail("Expected diagnostic collector to reject oversized body")
        } catch {
            XCTAssertEqual(error as? ResponseLineBufferError, .bodyTooLarge)
        }
    }
}
