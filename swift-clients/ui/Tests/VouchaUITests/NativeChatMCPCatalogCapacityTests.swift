import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPCatalogCapacityTests: XCTestCase {
    func testOversizedCatalogCannotPublishConnectedSession() async throws {
        let service = CapacityMCPService(count: 129)
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")

        do {
            try await session.connect()
            XCTFail("Unsupported catalog size must fail before connection publication")
        } catch NativeChatMCPNetworkService.Failure.invalidToolCatalog {}

        XCTAssertEqual(session.status, .failed)
        XCTAssertNil(session.connectedSnapshot())
        XCTAssertNil(session.toolCatalog)
    }

    func testMaximumCatalogIsUsableAndOversizedRefreshInvalidatesIt() async throws {
        let service = CapacityMCPService(count: 128)
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let previous = try XCTUnwrap(session.connectedSnapshot())
        XCTAssertEqual(try NativeChatMCPToolBridge(catalog: previous.catalog).tools.count, 128)
        service.catalog = CapacityMCPService.catalog(count: 129)

        do {
            try await session.refreshTools()
            XCTFail("An unusable refreshed catalog must invalidate the earlier snapshot")
        } catch NativeChatMCPNetworkService.Failure.invalidToolCatalog {}

        XCTAssertEqual(session.status, .failed)
        XCTAssertNil(session.connectedSnapshot())
        XCTAssertNil(session.toolCatalog)
        XCTAssertFalse(session.isCurrent(previous))
    }
}

@MainActor
private final class CapacityMCPService: NativeChatMCPServicing {
    var catalog: DecodedJSONValue

    init(count: Int) {
        catalog = Self.catalog(count: count)
    }

    static func catalog(count: Int) -> DecodedJSONValue {
        .object(["tools": .array((0 ..< count).map { index in
            .object([
                "name": .string("tool_\(index)"),
                "inputSchema": .object(["type": .string("object")])
            ])
        })])
    }

    func connect(accountId _: String) async throws -> DecodedJSONValue {
        catalog
    }

    func refreshTools(accountId _: String) async throws -> DecodedJSONValue {
        catalog
    }

    func disconnect(accountId _: String) async throws {}
}
