import Foundation

private enum FilamentsContractRoot {
    static func url() throws -> URL {
        let environment = ProcessInfo.processInfo.environment
        let configuredValue = environment["VOUCHA_FILAMENTS_CONTRACT_ROOT"]?.trimmingCharacters(
            in: .whitespacesAndNewlines
        )
        guard
            let value = configuredValue,
            !value.isEmpty
        else {
            throw LocalLLMEndpointPolicyError.invalid(
                "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
            )
        }

        let root = URL(fileURLWithPath: value).standardizedFileURL
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw LocalLLMEndpointPolicyError.invalid(
                "VOUCHA_FILAMENTS_CONTRACT_ROOT must name an existing directory, got \(root.path)."
            )
        }
        for path in [
            "api-fixtures/v1/local-llm-endpoint-policy.json",
            "api-fixtures/v1/local-llm-endpoint-policy.schema.json"
        ] {
            guard FileManager.default.fileExists(atPath: root.appendingPathComponent(path).path) else {
                throw LocalLLMEndpointPolicyError.invalid("Filaments contract root is missing required path \(path).")
            }
        }
        return root
    }
}

struct LocalLLMEndpointPolicyContract: Decodable {
    let hostPolicyRows: [LocalLLMHostPolicyRow]
    let originPairs: [LocalLLMOriginPair]

    static func load() throws -> Self {
        let contract = try FilamentsContractRoot.url()
            .appendingPathComponent("api-fixtures/v1/local-llm-endpoint-policy.json")
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

enum LocalLLMEndpointPolicyError: Error, CustomStringConvertible {
    case invalid(String)

    var description: String {
        switch self {
        case let .invalid(message):
            message
        }
    }
}
