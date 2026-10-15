import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        func projectNull(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["type", "enum", "const", "$defs"])
            if let constant = fields["const"], !(constant is NSNull) {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            if let values = fields["enum"] {
                guard let choices = values as? [Any], !choices.isEmpty,
                      choices.contains(where: { $0 is NSNull }) else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
            }
            return .null
        }

        func validateRootObjectAlternatives(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws {
            guard rootIsObjectShaped(
                fields, definitions: definitions, activeReferences: [], allowUnconstrainedRoot: true
            ) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
        }

        private func rootIsObjectShaped(
            _ fields: [String: Any], definitions: [String: [String: Any]], activeReferences: Set<String>,
            allowUnconstrainedRoot: Bool
        ) -> Bool {
            if let path = fields["$ref"] as? String {
                guard path.hasPrefix("#/$defs/") else { return false }
                let key = String(path.dropFirst(8))
                guard let target = definitions[key], !activeReferences.contains(key) else { return false }
                return rootIsObjectShaped(
                    target,
                    definitions: definitions,
                    activeReferences: activeReferences.union([key]),
                    allowUnconstrainedRoot: false
                )
            }
            for key in ["anyOf", "oneOf"] {
                if let branches = fields[key] as? [[String: Any]] {
                    return !branches.isEmpty && branches.allSatisfy {
                        rootIsObjectShaped(
                            $0, definitions: definitions, activeReferences: activeReferences,
                            allowUnconstrainedRoot: false
                        )
                    }
                }
            }
            if let types = fields["type"] as? [String] { return !types.isEmpty && types.allSatisfy { $0 == "object" } }
            if let type = fields["type"] as? String { return type == "object" }
            if fields["enum"] != nil || fields["const"] != nil { return false }
            let inferred = NativeChatFoundationModelsSchema.inferredType(fields)
            return inferred == "object" || (allowUnconstrainedRoot && inferred.isEmpty)
        }

        mutating func projectDependencies(
            definitions: [String: [String: Any]], reachable: Set<String>
        ) throws -> [DynamicGenerationSchema] {
            try reachable.sorted().map { key in
                guard let value = definitions[key] else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                try NativeChatFoundationModelsSchema.validateStringConstraints(value)
                if value["properties"] is [String: [String: Any]] {
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
                    guard NativeChatFoundationModelsSchema.projectsOnlyDeclaredProperties(value)
                    else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                    let properties = projectedObjectProperties(value)
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
                    try NativeChatFoundationModelsSchema.validateKeywords(
                        value,
                        allowing: ["type", "enum", "minLength", "maxLength", "pattern", "format"]
                    )
                    return .init(name: key, anyOf: values)
                }
                return try .init(name: key, anyOf: [project(value)])
            }
        }
    }
#endif
