import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    /// Projects the advertised MCP input shape into the schema accepted by FoundationModels.
    /// The original schema remains authoritative at the call boundary.
    @available(macOS 26.4, iOS 26.4, *)
    enum NativeChatFoundationModelsSchema {
        enum Failure: Error { case unsupported }

        static func project(_ data: Data) throws -> GenerationSchema {
            let root = try projectionFields(from: data)
            var projection = Projection()
            return try projection.schema(root)
        }

        struct Projection {
            private var nextID = 0
            private var reservedNames: Set<String> = []

            mutating func name() -> String {
                while true {
                    nextID += 1
                    let candidate = "MCPNode_\(nextID)"
                    if !reservedNames.contains(candidate) { return candidate }
                }
            }

            mutating func schema(_ fields: [String: Any]) throws -> GenerationSchema {
                let definitions = fields["$defs"] as? [String: [String: Any]] ?? [:]
                let reachable = try reachableDefinitions(in: fields, definitions: definitions)
                reservedNames = reachable
                guard try minimumOutputNodes(fields, definitions: definitions) <= 256 else {
                    throw Failure.unsupported
                }
                guard try minimumOutputBytes(fields, definitions: definitions) <= 4_096 else {
                    throw Failure.unsupported
                }
                try validateRootObjectAlternatives(fields, definitions: definitions)
                let dependencies = try projectDependencies(definitions: definitions, reachable: reachable)
                if NativeChatFoundationModelsSchema.inferredType(fields).isEmpty,
                   fields["type"] == nil, fields["enum"] == nil,
                   fields["$ref"] == nil, fields["anyOf"] == nil, fields["oneOf"] == nil {
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["$defs"])
                    return try GenerationSchema(root: .init(name: name(), properties: []), dependencies: dependencies)
                }
                return try GenerationSchema(root: project(fields), dependencies: dependencies)
            }

            mutating func project(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                try NativeChatFoundationModelsSchema.validateConstant(fields)
                try NativeChatFoundationModelsSchema.validateStringConstraints(fields)
                if NativeChatFoundationModelsSchema.isIntegerType(fields) {
                    try NativeChatFoundationModelsSchema.validateSafeIntegerRange(fields)
                }
                if let alternative = try projectReferenceOrChoices(fields) { return alternative }
                if let types = fields["type"] as? [String] {
                    return try projectTypeUnion(fields, types: types)
                }
                if fields["type"] == nil, let values = fields["enum"] as? [Any] {
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["enum", "$defs"])
                    let strings = values.compactMap { $0 as? String }
                    let hasNull = values.contains { $0 is NSNull }
                    guard strings.count + (hasNull ? 1 : 0) == values.count else { throw Failure.unsupported }
                    if !hasNull { return .init(name: name(), anyOf: strings) }
                    if strings.isEmpty { return .null }
                    return .init(name: name(), anyOf: [.init(name: name(), anyOf: strings), .null])
                }
                if NativeChatFoundationModelsSchema.inferredType(fields).isEmpty,
                   fields["type"] == nil {
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["$defs"])
                    return .init(type: GeneratedContent.self)
                }
                return try projectType(fields)
            }

            private mutating func projectType(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                switch fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) {
                case "object": return try projectObject(fields)
                case "array": return try projectArray(fields)
                case "string":
                    try NativeChatFoundationModelsSchema.validateKeywords(
                        fields,
                        allowing: ["type", "enum", "const", "$defs"]
                    )
                    guard fields["enum"] == nil || fields["const"] == nil else { throw Failure.unsupported }
                    return try projectString(fields)
                case "integer": return try projectInteger(fields)
                case "number": return try projectNumber(fields)
                case "boolean":
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["type", "$defs"])
                    return .init(type: Bool.self)
                case "null": return try projectNull(fields)
                default: throw Failure.unsupported
                }
            }

            private mutating func projectObject(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                try NativeChatFoundationModelsSchema.validateKeywords(
                    fields, allowing: [
                        "type",
                        "properties",
                        "required",
                        "additionalProperties",
                        "minProperties",
                        "maxProperties",
                        "$defs"
                    ]
                )
                guard fields["additionalProperties"] == nil || fields["additionalProperties"] as? Bool == false else {
                    throw Failure.unsupported
                }
                let properties = fields["properties"] as? [String: [String: Any]] ?? [:]
                let required = Set(fields["required"] as? [String] ?? [])
                guard required.isSubset(of: properties.keys) else { throw Failure.unsupported }
                try validateObjectBounds(fields, requiredCount: required.count, propertyCount: properties.count)
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

        }
    }
#endif
