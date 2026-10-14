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

    func testConnectedDuplicateCatalogUsesToolFreeAppleFallbackWithoutAdmittingRPC() async throws {
        let service = CapacityMCPService(count: 2)
        service.catalog = .object(["tools": .array([
            Self.tool(named: "duplicate"), Self.tool(named: "duplicate")
        ])])
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        var fallbackCalls = 0
        let admission = try await NativeChatAppleFoundationModelsProvider.admitMCPBridge(
            catalog: snapshot.catalog
        ) {
            fallbackCalls += 1
            return .init(content: "Tool-free answer", modelProvider: "apple_foundation", modelName: nil)
        }
        guard case let .toolFree(response) = admission else {
            return XCTFail("A connected but bridge-invalid catalog must use tool-free generation")
        }
        XCTAssertEqual(fallbackCalls, 1)
        XCTAssertEqual(response?.content, "Tool-free answer")
        XCTAssertEqual(response?.admittedMCPCall, false)
        XCTAssertNil(response?.persistenceSnapshot(snapshot, providerSelection: .appleFoundationModels))
        XCTAssertTrue(session.isCurrent(snapshot))
        await session.useAccount("member-b")
        XCTAssertFalse(session.isCurrent(snapshot))
    }

    func testBridgeAdmissionKeepsValidCatalogToolEnabledAndCancellationStopsFallback() async throws {
        let catalog = CapacityMCPService.catalog(count: 1)
        let admitted = try await NativeChatAppleFoundationModelsProvider.admitMCPBridge(catalog: catalog) {
            XCTFail("Valid catalog should not invoke tool-free generation")
            return nil
        }
        guard case let .enabled(bridge) = admitted else { return XCTFail("Expected bridge admission") }
        XCTAssertEqual(bridge.tools.count, 1)

        let cancelled = Task { () throws in
            withUnsafeCurrentTask { $0?.cancel() }
            _ = try await NativeChatAppleFoundationModelsProvider.admitMCPBridge(
                catalog: .object(["tools": .array([Self.tool(named: "bad.name")])])
            ) {
                XCTFail("Cancelled turn must not start tool-free generation")
                return nil
            }
        }
        do {
            try await cancelled.value
            XCTFail("Cancellation must propagate before fallback")
        } catch is CancellationError {}
    }

    private static func tool(named name: String) -> DecodedJSONValue {
        .object([
            "name": .string(name),
            "inputSchema": .object(["type": .string("object")])
        ])
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
