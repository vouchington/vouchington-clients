import Foundation
import VouchaModels

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func projectionFields(from data: Data) throws -> [String: Any] {
            guard case let .object(fields) = try JSONDecoder().decode(DecodedJSONValue.self, from: data) else {
                throw Failure.unsupported
            }
            return try normalizeSingletonAllOf(fields.mapValues(projectionValue))
        }

        private static func projectionValue(_ value: DecodedJSONValue) -> Any {
            switch value {
            case .null: NSNull()
            case let .bool(value): value
            case let .number(value): NSNumber(value: value)
            case let .integer(value): NSNumber(value: value)
            case let .unsignedInteger(value): NSNumber(value: value)
            case let .decimal(value): NSDecimalNumber(decimal: value)
            case let .string(value): value
            case let .array(values): values.map(projectionValue)
            case let .object(fields): fields.mapValues(projectionValue)
            }
        }

        static func inferredType(_ fields: [String: Any]) -> String {
            if fields["const"] is NSNull { return "null" }
            if fields["const"] is String { return "string" }
            if let constant = fields["const"], (try? safeIntegerConstant(constant)) != nil { return "integer" }
            if let constant = fields["const"], (try? safeNumberConstant(constant)) != nil { return "number" }
            if let values = fields["enum"] as? [Any], !values.isEmpty {
                if values.allSatisfy({ $0 is String }) { return "string" }
                if values.allSatisfy({ $0 is NSNull }) { return "null" }
            }
            if fields["properties"] != nil { return "object" }
            if fields["items"] != nil { return "array" }
            return ""
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        func reachableDefinitions(
            in root: [String: Any], definitions: [String: [String: Any]]
        ) throws -> Set<String> {
            var pending: [String] = []
            try collectReferences(in: root, into: &pending, depth: 0)
            var reachable: Set<String> = []
            while let name = pending.popLast() {
                guard let definition = definitions[name] else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                guard reachable.insert(name).inserted else { continue }
                try collectReferences(in: definition, into: &pending, depth: 0)
            }
            return reachable
        }

        private func collectReferences(in value: Any, into pending: inout [String], depth: Int) throws {
            guard depth <= 32 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if let fields = value as? [String: Any] {
                if let path = fields["$ref"] as? String {
                    guard path.hasPrefix("#/$defs/"), path.count > 8 else {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
                    pending.append(String(path.dropFirst(8)))
                }
                let projectsObject = fields["type"] as? String == "object"
                    || (fields["type"] == nil && NativeChatFoundationModelsSchema.inferredType(fields) == "object")
                if projectsObject, fields["maxProperties"] as? Int != 0,
                   let properties = fields["properties"] as? [String: Any] {
                    for child in properties.values {
                        try collectReferences(in: child, into: &pending, depth: depth + 1)
                    }
                }
                let projectsArray = fields["type"] as? String == "array"
                    || (fields["type"] == nil && NativeChatFoundationModelsSchema.inferredType(fields) == "array")
                if projectsArray, fields["maxItems"] as? Int != 0, let items = fields["items"] {
                    try collectReferences(in: items, into: &pending, depth: depth + 1)
                }
                for key in ["anyOf", "oneOf"] {
                    if let choices = fields[key] as? [Any] {
                        for child in choices {
                            try collectReferences(in: child, into: &pending, depth: depth + 1)
                        }
                    }
                }
            }
        }
    }
#endif
