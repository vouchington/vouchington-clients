import Foundation
import VouchaTestSupport

struct LocalLLMEndpointPolicyContract: Decodable {
    let hostPolicyRows: [LocalLLMHostPolicyRow]
    let originPairs: [LocalLLMOriginPair]

    static func load() throws -> Self {
        try load(root: FilamentsContractRoot.url())
    }

    static func load(root: URL) throws -> Self {
        let contract = try FilamentsContractRoot.requiredURL(
            "api-fixtures/v1/local-llm-endpoint-policy.json",
            root: root
        )
        _ = try FilamentsContractRoot.requiredURL(
            "api-fixtures/v1/local-llm-endpoint-policy.schema.json",
            root: root
        )
        return try JSONDecoder().decode(Self.self, from: Data(contentsOf: contract))
    }

    func hostPolicyRows(for consumer: String) -> [LocalLLMHostPolicyRow] {
        guard let parsed = LocalLLMEndpointPolicyConsumer(rawValue: consumer) else {
            return []
        }
        return hostPolicyRows.filter { $0.requiredConsumers.contains(parsed) }
    }

    func originPairs(for consumer: String) -> [LocalLLMOriginPair] {
        guard let parsed = LocalLLMEndpointPolicyConsumer(rawValue: consumer) else {
            return []
        }
        return originPairs.filter { $0.requiredConsumers.contains(parsed) }
    }
}

enum LocalLLMEndpointPolicyConsumer: String, Decodable {
    case dotnetCore = "dotnet-core"
    case swiftCore = "swift-core"
    case swiftAndroid = "swift-android"
}

enum LocalLLMHostPolicyVerdict: String, Decodable {
    case allowed
    case rejected
}

struct LocalLLMHostPolicyRow: Decodable {
    let id: String
    let requiredConsumers: [LocalLLMEndpointPolicyConsumer]
    let endpoint: String
    let verdict: LocalLLMHostPolicyVerdict
    let notes: String

    var isAllowed: Bool {
        verdict == .allowed
    }
}

struct LocalLLMOriginPair: Decodable {
    let id: String
    let requiredConsumers: [LocalLLMEndpointPolicyConsumer]
    let variantA: String
    let variantB: String
    let expectedOrigin: String
    let notes: String
}
