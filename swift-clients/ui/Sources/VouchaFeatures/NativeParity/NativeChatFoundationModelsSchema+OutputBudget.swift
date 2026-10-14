import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        /// A conservative lower bound on JSON value nodes the model must emit.
        /// Array minima multiply through nested items rather than applying a per-node cap alone.
        func minimumOutputNodes(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws -> Int {
            try minimumOutputNodes(fields, definitions: definitions, activeReferences: [])
        }

        private func minimumOutputNodes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            if let path = fields["$ref"] as? String {
                return try minimumReferenceNodes(path, definitions: definitions, activeReferences: activeReferences)
            }
            for choice in ["anyOf", "oneOf"] {
                if let branches = fields[choice] as? [[String: Any]] {
                    return try minimumChoiceNodes(
                        branches,
                        definitions: definitions,
                        activeReferences: activeReferences
                    )
                }
            }
            if let types = fields["type"] as? [String] {
                return try minimumChoiceNodes(
                    types.map { ["type": $0] }, definitions: definitions, activeReferences: activeReferences
                )
            }
            switch fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) {
            case "array":
                return try minimumArrayNodes(fields, definitions: definitions, activeReferences: activeReferences)
            case "object":
                return try minimumObjectNodes(fields, definitions: definitions, activeReferences: activeReferences)
            default:
                return 1
            }
        }

        private func minimumReferenceNodes(
            _ path: String,
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            guard path.hasPrefix("#/$defs/") else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            let key = String(path.dropFirst(8))
            guard let target = definitions[key], !activeReferences.contains(key) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return try minimumOutputNodes(
                target, definitions: definitions, activeReferences: activeReferences.union([key])
            )
        }

        private func minimumChoiceNodes(
            _ branches: [[String: Any]],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            guard !branches.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return try branches.map {
                try minimumOutputNodes($0, definitions: definitions, activeReferences: activeReferences)
            }.min() ?? 1
        }

        private func minimumArrayNodes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            guard let item = fields["items"] as? [String: Any] else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            let minimum = fields["minItems"] as? Int ?? 0
            guard (0 ... 64).contains(minimum) else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if minimum == 0 { return 1 }
            let itemNodes = try minimumOutputNodes(
                item, definitions: definitions, activeReferences: activeReferences
            )
            guard itemNodes <= 256 / minimum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            let total = 1 + minimum * itemNodes
            guard total <= 256 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return total
        }

        private func minimumObjectNodes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            let properties = fields["properties"] as? [String: [String: Any]] ?? [:]
            let required = fields["required"] as? [String] ?? []
            var total = 1
            for key in Set(required) {
                guard let property = properties[key] else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                total += try minimumOutputNodes(
                    property, definitions: definitions, activeReferences: activeReferences
                )
                guard total <= 256 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            }
            return total
        }
    }
#endif
