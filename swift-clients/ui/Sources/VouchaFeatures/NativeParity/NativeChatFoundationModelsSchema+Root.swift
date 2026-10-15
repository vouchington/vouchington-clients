import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        mutating func projectDependencies(
            definitions: [String: [String: Any]], reachable: Set<String>
        ) throws -> [DynamicGenerationSchema] {
            try reachable.sorted().map { key in
                guard let value = definitions[key] else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                try NativeChatFoundationModelsSchema.validateStringConstraints(value)
                if let properties = value["properties"] as? [String: [String: Any]] {
                    guard value["type"] == nil || value["type"] as? String == "object" else {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
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
                    else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                    let required = Set(value["required"] as? [String] ?? [])
                    guard required.isSubset(of: properties.keys) else {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
                    try validateObjectBounds(value, requiredCount: required.count, propertyCount: properties.count)
                    return try .init(name: key, properties: properties.keys.sorted().map { field in
                        guard let property = properties[field] else {
                            throw NativeChatFoundationModelsSchema.Failure.unsupported
                        }
                        return try .init(
                            name: field,
                            description: property["description"] as? String,
                            schema: project(property),
                            isOptional: !required.contains(field)
                        )
                    })
                }
                if let values = value["enum"] as? [String] {
                    guard value["type"] == nil || value["type"] as? String == "string" else {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
                    try NativeChatFoundationModelsSchema.validateKeywords(value, allowing: ["type", "enum"])
                    return .init(name: key, anyOf: values)
                }
                return try .init(name: key, anyOf: [project(value)])
            }
        }
    }
#endif
