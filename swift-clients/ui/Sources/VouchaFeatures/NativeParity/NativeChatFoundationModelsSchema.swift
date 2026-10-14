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

        struct Projection {
            private var nextID = 0

            mutating func name() -> String {
                nextID += 1
                return "MCPNode_\(nextID)"
            }

            mutating func schema(_ fields: [String: Any]) throws -> GenerationSchema {
                let definitions = fields["$defs"] as? [String: [String: Any]] ?? [:]
                let dependencies = try definitions.keys.sorted().map { key -> DynamicGenerationSchema in
                    guard let value = definitions[key] else { throw Failure.unsupported }
                    try NativeChatFoundationModelsSchema.validateStringConstraints(value)
                    if let properties = value["properties"] as? [String: [String: Any]] {
                        try NativeChatFoundationModelsSchema.validateKeywords(
                            value, allowing: [
                                "type",
                                "properties",
                                "required",
                                "additionalProperties",
                                "minProperties",
                                "maxProperties"
                            ]
                        )
                        guard value["additionalProperties"] == nil || value["additionalProperties"] as? Bool == false
                        else { throw Failure.unsupported }
                        let required = Set(value["required"] as? [String] ?? [])
                        guard required.isSubset(of: properties.keys) else { throw Failure.unsupported }
                        try validateObjectBounds(value, requiredCount: required.count, propertyCount: properties.count)
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
                    if let values = value["enum"] as? [String] {
                        try NativeChatFoundationModelsSchema.validateKeywords(value, allowing: ["type", "enum"])
                        return .init(name: key, anyOf: values)
                    }
                    return try .init(name: key, anyOf: [project(value)])
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
                    return .init(name: name(), anyOf: [.init(name: name(), anyOf: strings), .null])
                }
                if fields.isEmpty { return .init(type: GeneratedContent.self) }
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
                    return projectString(fields)
                case "integer": return try projectInteger(fields)
                case "number": return try projectNumber(fields)
                case "boolean":
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["type", "$defs"])
                    return .init(type: Bool.self)
                case "null":
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["type", "$defs"])
                    return .null
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

            private mutating func projectArray(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
                try NativeChatFoundationModelsSchema.validateKeywords(
                    fields, allowing: ["type", "items", "minItems", "maxItems", "uniqueItems", "$defs"]
                )
                guard fields["uniqueItems"] == nil || fields["uniqueItems"] as? Bool == false else {
                    throw Failure.unsupported
                }
                guard let item = fields["items"] as? [String: Any] else { throw Failure.unsupported }
                return try .init(
                    arrayOf: project(item),
                    minimumElements: fields["minItems"] as? Int,
                    maximumElements: fields["maxItems"] as? Int
                )
            }

        }
    }
#endif
