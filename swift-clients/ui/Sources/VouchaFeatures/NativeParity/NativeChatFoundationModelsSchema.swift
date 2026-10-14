import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    /// Projects the advertised MCP input shape into the schema accepted by FoundationModels.
    /// The original schema remains authoritative at the call boundary.
    @available(macOS 26.4, iOS 26.4, *)
    enum NativeChatFoundationModelsSchema {
        enum Failure: Error { case unsupported }

        static func inferredType(_ fields: [String: Any]) -> String {
            if fields["properties"] != nil { return "object" }
            if fields["items"] != nil { return "array" }
            if fields["const"] is String { return "string" }
            return ""
        }

        static func project(_ data: Data) throws -> GenerationSchema {
            guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                throw Failure.unsupported
            }
            var projection = Projection()
            return try projection.schema(root)
        }

        private struct Projection {
            private var nextID = 0

            private mutating func name() -> String {
                nextID += 1
                return "MCPNode_\(nextID)"
            }

            mutating func schema(_ fields: [String: Any]) throws -> GenerationSchema {
                let definitions = fields["$defs"] as? [String: [String: Any]] ?? [:]
                let dependencies = try definitions.keys.sorted().map { key -> DynamicGenerationSchema in
                    guard let value = definitions[key] else { throw Failure.unsupported }
                    try NativeChatFoundationModelsSchema.validateStringConstraints(value)
                    if let properties = value["properties"] as? [String: [String: Any]] {
                        guard value["additionalProperties"] == nil || value["additionalProperties"] as? Bool == false
                        else { throw Failure.unsupported }
                        guard (value["minProperties"] as? Int ?? 0) <= properties.count else {
                            throw Failure.unsupported
                        }
                        let required = Set(value["required"] as? [String] ?? [])
                        guard required.isSubset(of: properties.keys) else { throw Failure.unsupported }
                        return try .init(name: key, properties: properties.keys.sorted().map { field in
                            guard let property = properties[field] else { throw Failure.unsupported }
                            return try .init(
                                name: field,
                                description: property["description"] as? String,
                                schema: project(property),
                                isOptional: !required.contains(field)
                            )
                        })
                    }
                    if let values = value["enum"] as? [String] { return .init(name: key, anyOf: values) }
                    return try .init(name: key, anyOf: [project(value)])
                }
                return try GenerationSchema(root: project(fields), dependencies: dependencies)
            }

            private mutating func project(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                try NativeChatFoundationModelsSchema.validateConstant(fields)
                try NativeChatFoundationModelsSchema.validateStringConstraints(fields)
                if NativeChatFoundationModelsSchema.isIntegerType(fields) {
                    try NativeChatFoundationModelsSchema.validateSafeIntegerRange(fields)
                }
                if let path = fields["$ref"] as? String {
                    guard path.hasPrefix("#/$defs/") else { throw Failure.unsupported }
                    return .init(referenceTo: String(path.dropFirst(8)))
                }
                for key in ["anyOf", "oneOf"] {
                    if let choices = fields[key] as? [[String: Any]] {
                        return try .init(name: name(), anyOf: choices.map { try project($0) })
                    }
                }
                if let types = fields["type"] as? [String] {
                    return try projectTypeUnion(fields, types: types)
                }
                if fields["type"] == nil, let values = fields["enum"] as? [Any] {
                    let strings = values.compactMap { $0 as? String }
                    let hasNull = values.contains { $0 is NSNull }
                    guard strings.count + (hasNull ? 1 : 0) == values.count else { throw Failure.unsupported }
                    if !hasNull { return .init(name: name(), anyOf: strings) }
                    return .init(name: name(), anyOf: [.init(name: name(), anyOf: strings), .null])
                }
                if fields.isEmpty { return .init(type: GeneratedContent.self) }
                return try projectType(fields)
            }

            private mutating func projectTypeUnion(
                _ fields: [String: Any], types: [String]
            ) throws -> DynamicGenerationSchema {
                guard Set(fields.keys).isSubset(of: ["type", "description", "title"]) else {
                    throw Failure.unsupported
                }
                return try .init(name: name(), anyOf: types.map { try project(["type": $0]) })
            }

            private mutating func projectType(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                switch fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) {
                case "object": return try projectObject(fields)
                case "array": return try projectArray(fields)
                case "string": return projectString(fields)
                case "integer": return .init(type: Int.self)
                case "number": return .init(type: Decimal.self)
                case "boolean": return .init(type: Bool.self)
                case "null": return .null
                default: throw Failure.unsupported
                }
            }

            private mutating func projectObject(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                guard fields["additionalProperties"] == nil || fields["additionalProperties"] as? Bool == false else {
                    throw Failure.unsupported
                }
                let properties = fields["properties"] as? [String: [String: Any]] ?? [:]
                guard (fields["minProperties"] as? Int ?? 0) <= properties.count else {
                    throw Failure.unsupported
                }
                let required = Set(fields["required"] as? [String] ?? [])
                guard required.isSubset(of: properties.keys) else { throw Failure.unsupported }
                return try .init(name: name(), properties: properties.keys.sorted().map { key in
                    guard let property = properties[key] else { throw Failure.unsupported }
                    return try .init(
                        name: key,
                        description: property["description"] as? String,
                        schema: project(property),
                        isOptional: !required.contains(key)
                    )
                })
            }

            private mutating func projectArray(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                guard let item = fields["items"] as? [String: Any] else { throw Failure.unsupported }
                return try .init(
                    arrayOf: project(item),
                    minimumElements: fields["minItems"] as? Int,
                    maximumElements: fields["maxItems"] as? Int
                )
            }

            private mutating func projectString(_ fields: [String: Any]) -> DynamicGenerationSchema {
                if let values = fields["enum"] as? [String] { return .init(name: name(), anyOf: values) }
                if let value = fields["const"] as? String { return .init(name: name(), anyOf: [value]) }
                return .init(type: String.self)
            }
        }
    }
#endif
