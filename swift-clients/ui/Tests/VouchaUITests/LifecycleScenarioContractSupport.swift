import Foundation

struct LifecycleScenarioContract: Decodable {
    let scenarios: [LifecycleScenario]
    let claims: [LifecycleClaim]

    static func load(file: StaticString = #filePath) throws -> Self {
        var directory = URL(fileURLWithPath: "\(file)").deletingLastPathComponent()
        for _ in 0 ..< 12 {
            let candidate = directory.appendingPathComponent("api-fixtures/v1/lifecycle-scenarios.json")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try JSONDecoder().decode(Self.self, from: Data(contentsOf: candidate))
            }
            directory.deleteLastPathComponent()
        }
        throw LifecycleScenarioError.invalid("Could not locate lifecycle-scenarios.json")
    }

    func scenarios(claimedBy consumer: String) throws -> [LifecycleScenario] {
        let claimed = Set(claims.lazy.filter { $0.consumer == consumer }.map(\.scenarioId))
        let required = Set(scenarios.lazy.filter { $0.requiredConsumers.contains(consumer) }.map(\.id))
        guard claimed == required else {
            throw LifecycleScenarioError.invalid("\(consumer) scenario claims do not match required scenarios")
        }
        return scenarios.filter { claimed.contains($0.id) }
    }

    func claim(for scenario: LifecycleScenario, consumer: String) throws -> LifecycleClaim {
        let matches = claims.filter { $0.scenarioId == scenario.id && $0.consumer == consumer }
        guard let claim = matches.only else {
            throw LifecycleScenarioError.invalid("Missing or duplicate \(consumer) claim for \(scenario.id)")
        }
        return claim
    }
}

struct LifecycleScenario: Decodable {
    let id: String
    let family: String
    let requiredConsumers: [String]
    let input: LifecycleScenarioInput
    let expected: LifecycleObservation
}

struct LifecycleClaim: Decodable {
    let scenarioId: String
    let consumer: String
    let adapter: String
}

private extension Collection {
    var only: Element? {
        count == 1 ? first : nil
    }
}

struct LifecycleScenarioInput: Decodable {
    let preconditions: [String: LifecycleJSON]
    let action: [String: LifecycleJSON]
    let serverOutcome: [String: LifecycleJSON]
}

struct LifecycleObservation: Decodable, Equatable {
    let visibleState: [String: LifecycleJSON]
    let availableActions: [String]
    let reconciliation: [String: LifecycleJSON]
    let cancellation: [String: LifecycleJSON]
}

enum LifecycleJSON: Decodable, Equatable {
    case string(String)
    case bool(Bool)
    case number(Double)
    case array([LifecycleJSON])
    case object([String: LifecycleJSON])
    case null

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([LifecycleJSON].self) {
            self = .array(value)
        } else {
            self = try .object(container.decode([String: LifecycleJSON].self))
        }
    }
}

enum LifecycleScenarioError: Error, CustomStringConvertible {
    case invalid(String)

    var description: String {
        switch self { case let .invalid(message): message }
    }
}

extension [String: LifecycleJSON] {
    func string(_ key: String) throws -> String {
        guard case let .string(value)? = self[key] else {
            throw LifecycleScenarioError.invalid("Missing string field \(key)")
        }
        return value
    }

    func optionalString(_ key: String) throws -> String? {
        switch self[key] {
        case let .string(value): value
        case .null: nil
        default: throw LifecycleScenarioError.invalid("Missing nullable string field \(key)")
        }
    }

    func strings(_ key: String) throws -> [String] {
        guard case let .array(values)? = self[key] else {
            throw LifecycleScenarioError.invalid("Missing string array field \(key)")
        }
        return try values.map {
            guard case let .string(value) = $0 else {
                throw LifecycleScenarioError.invalid("Non-string value in \(key)")
            }
            return value
        }
    }

    func bool(_ key: String) throws -> Bool {
        guard case let .bool(value)? = self[key] else {
            throw LifecycleScenarioError.invalid("Missing boolean field \(key)")
        }
        return value
    }

    func number(_ key: String) throws -> Double {
        guard case let .number(value)? = self[key] else {
            throw LifecycleScenarioError.invalid("Missing number field \(key)")
        }
        return value
    }
}

let lifecycleNotApplicable: [String: LifecycleJSON] = ["behavior": .string("not-applicable")]
