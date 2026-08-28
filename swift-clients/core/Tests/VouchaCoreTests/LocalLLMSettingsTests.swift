import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMSettingsTests: XCTestCase {
    func testResponsesURLNormalizesBaseEndpoints() {
        XCTAssertEqual(
            LocalLLMEndpointProfile.responsesURL(from: "http://localhost:11434")?.absoluteString,
            "http://localhost:11434/v1/responses"
        )
        XCTAssertEqual(
            LocalLLMEndpointProfile.responsesURL(from: "http://localhost:1234/v1")?.absoluteString,
            "http://localhost:1234/v1/responses"
        )
        XCTAssertEqual(
            LocalLLMEndpointProfile.responsesURL(from: "http://localhost:1234/v1/responses")?.absoluteString,
            "http://localhost:1234/v1/responses"
        )
        XCTAssertEqual(
            LocalLLMEndpointProfile.responsesURL(from: "https://models.example.test/openai")?.absoluteString,
            "https://models.example.test/openai/responses"
        )
    }

    func testResponsesURLAppliesTheSharedEndpointPolicy() throws {
        let contract = try LocalLLMEndpointPolicyContract.load()
        let rows = contract.hostPolicyRows(for: "swift-core")
        XCTAssertEqual(Set(rows.map(\.id)), Self.expectedHostPolicyRowIDs)

        for row in rows {
            let result = LocalLLMEndpointProfile.responsesURL(from: row.endpoint)
            if row.isAllowed {
                XCTAssertNotNil(result, "\(row.id): \(row.notes)")
            } else {
                XCTAssertNil(result, "\(row.id): \(row.notes)")
            }
        }
    }

    func testHostPolicyRowRejectsUnknownVerdict() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-core"],"endpoint":"http://127.0.0.1:11434","verdict":"rejetced","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }

    func testHostPolicyRowRejectsUnknownConsumer() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-croe"],"endpoint":"http://127.0.0.1:11434","verdict":"allowed","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }

    func testEndpointOriginAppliesTheSharedCanonicalizationPolicy() throws {
        let contract = try LocalLLMEndpointPolicyContract.load()
        let pairs = contract.originPairs(for: "swift-core")
        XCTAssertEqual(Set(pairs.map(\.id)), Self.expectedOriginPairIDs)

        for pair in pairs {
            let originA = LocalLLMEndpointProfile(endpoint: pair.variantA).origin
            let originB = LocalLLMEndpointProfile(endpoint: pair.variantB).origin
            XCTAssertEqual(originA, pair.expectedOrigin, "\(pair.id) variantA: \(pair.notes)")
            XCTAssertEqual(originB, pair.expectedOrigin, "\(pair.id) variantB: \(pair.notes)")
        }
    }

    func testEndpointPolicyRejectsSymbolicLinkedContractRootAndComponents() throws {
        let root = try makeEndpointPolicyContractRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        let linkedRoot = root.appendingPathComponent("linked-root", isDirectory: true)
        try FileManager.default.createSymbolicLink(at: linkedRoot, withDestinationURL: root)
        XCTAssertThrowsError(try LocalLLMEndpointPolicyContract.load(root: linkedRoot)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }

        let linkedApiFixtures = root.appendingPathComponent("linked-api-fixtures", isDirectory: true)
        try FileManager.default.moveItem(
            at: root.appendingPathComponent("api-fixtures", isDirectory: true),
            to: linkedApiFixtures
        )
        try FileManager.default.createSymbolicLink(
            at: root.appendingPathComponent("api-fixtures", isDirectory: true),
            withDestinationURL: linkedApiFixtures
        )
        XCTAssertThrowsError(try LocalLLMEndpointPolicyContract.load(root: root)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }
    }

    private static let expectedHostPolicyRowIDs: Set<String> = [
        "loopback-ipv4", "rfc1918-10-private", "rfc1918-172-private", "rfc1918-192-private",
        "link-local-ipv4", "loopback-ipv6", "ula-ipv6", "link-local-ipv6", "dot-local-hostname",
        "single-label-hostname", "localhost-name", "cgnat-100-64-rejected", "cgnat-100-128-rejected",
        "public-ipv4-rejected", "public-ipv6-rejected",
        "public-hostname-rejected", "zone-id-private-looking-rejected",
        "zone-id-percent-encoded-private-looking-rejected", "zone-id-public-rejected",
        "ipv4-mapped-loopback-rejected", "ipv4-mapped-private-rejected", "ipv4-mapped-public-rejected",
        "non-http-scheme-rejected", "malformed-url-rejected", "userinfo-present-rejected",
        "query-present-rejected", "fragment-present-rejected", "https-public-host-allowed",
        "https-cgnat-100-64-allowed", "https-zone-id-rejected"
    ]

    private static let expectedOriginPairIDs: Set<String> = [
        "https-default-vs-explicit-port", "http-default-vs-explicit-port",
        "ipv6-default-vs-explicit-port-http", "ipv6-default-vs-explicit-port-https"
    ]

    func testFeaturePolicyDefaultsOffOutsideMacOSButSupportsOverride() {
        XCTAssertTrue(LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]).isEnabled)
        XCTAssertFalse(LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "false"]).isEnabled)
        XCTAssertTrue(LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "YES"]).isEnabled)
    }

    func testFeaturePolicyIgnoresBlankOverride() {
        let defaultPolicy = LocalLLMFeaturePolicy(environment: [:])

        XCTAssertEqual(
            LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "   "]).isEnabled,
            defaultPolicy.isEnabled
        )
    }

    func testConfigurationNormalizesModelsAndSelectedModel() {
        let profile = LocalLLMEndpointProfile(
            isEnabled: true,
            endpoint: "http://localhost:11434",
            modelNames: [" gpt-oss ", "", "gpt-oss", "llama"],
            selectedModelName: " llama "
        )

        XCTAssertEqual(profile.normalizedModelNames, ["gpt-oss", "llama"])
        XCTAssertEqual(profile.selectedModel, "llama")
        XCTAssertNil(LocalLLMEndpointProfile(selectedModelName: " ").selectedModel)
    }

    func testConfigurationKeepsExplicitEndpointSelectionWithoutFallback() {
        let selectedID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [LocalLLMEndpointProfile(id: selectedID, endpoint: "https://models.example.test")],
            selectedEndpointID: selectedID
        )

        XCTAssertEqual(configuration.selectedEndpoint?.id, selectedID)
        XCTAssertNil(configuration.endpoint(id: UUID()))
    }

    private func makeEndpointPolicyContractRoot() throws -> URL {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-contract-root-\(UUID().uuidString)", isDirectory: true)
        let fixtureRoot = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        try FileManager.default.createDirectory(at: fixtureRoot, withIntermediateDirectories: true)
        try Data("{\"hostPolicyRows\":[],\"originPairs\":[]}".utf8).write(
            to: fixtureRoot.appendingPathComponent("local-llm-endpoint-policy.json")
        )
        try Data("{}".utf8).write(to: fixtureRoot.appendingPathComponent("local-llm-endpoint-policy.schema.json"))
        return root
    }
}
