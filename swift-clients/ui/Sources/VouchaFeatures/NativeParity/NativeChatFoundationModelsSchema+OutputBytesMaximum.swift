import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        /// Minimum bytes required if the model emits the largest projected shape and longest fixed literals.
        func maximumForcedOutputBytes(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws -> Int {
            try maximumForcedOutputBytes(fields, definitions: definitions, activeReferences: [])
        }

        private func maximumForcedOutputBytes(
            _ fields: [String: Any], definitions: [String: [String: Any]], activeReferences: Set<String>
        ) throws -> Int {
            if let path = fields["$ref"] as? String {
                return try maximumReferencedBytes(path, definitions: definitions, activeReferences: activeReferences)
            }
            for choice in ["anyOf", "oneOf"] {
                if let branches = fields[choice] as? [[String: Any]] {
                    return try largestChoiceBytes(
                        branches, definitions: definitions, activeReferences: activeReferences
                    )
                }
            }
            if let types = fields["type"] as? [String] {
                return try largestChoiceBytes(
                    types.map { ["type": $0] }, definitions: definitions, activeReferences: activeReferences
                )
            }
            switch fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) {
            case "array": return try maximumArrayBytes(
                    fields, definitions: definitions, activeReferences: activeReferences
                )
            case "object": return try maximumObjectBytes(
                    fields, definitions: definitions, activeReferences: activeReferences
                )
            default: return try maximumScalarBytes(fields)
            }
        }

        private func maximumReferencedBytes(
            _ path: String, definitions: [String: [String: Any]], activeReferences: Set<String>
        ) throws -> Int {
            guard path.hasPrefix("#/$defs/") else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            let key = String(path.dropFirst(8))
            guard let target = definitions[key], !activeReferences.contains(key), activeReferences.count < 31 else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return try maximumForcedOutputBytes(
                target, definitions: definitions, activeReferences: activeReferences.union([key])
            )
        }

        private func largestChoiceBytes(
            _ branches: [[String: Any]], definitions: [String: [String: Any]], activeReferences: Set<String>
        ) throws -> Int {
            guard !branches.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return try branches.map {
                try maximumForcedOutputBytes($0, definitions: definitions, activeReferences: activeReferences)
            }.max() ?? 1
        }

        private func maximumArrayBytes(
            _ fields: [String: Any], definitions: [String: [String: Any]], activeReferences: Set<String>
        ) throws -> Int {
            let item = fields["items"] as? [String: Any] ?? [:]
            let count = min(fields["maxItems"] as? Int ?? 64, 64)
            guard count >= 0 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if count == 0 { return 2 }
            let bytes = try maximumForcedOutputBytes(
                item, definitions: definitions, activeReferences: activeReferences
            )
            guard bytes <= (4_096 - count - 1) / count else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return 2 + count * bytes + count - 1
        }

        private func maximumObjectBytes(
            _ fields: [String: Any], definitions: [String: [String: Any]], activeReferences: Set<String>
        ) throws -> Int {
            let properties = projectedObjectProperties(fields)
            var total = 2
            for (key, property) in properties {
                let keyBytes = try serializedBytes(key)
                let valueBytes = try maximumForcedOutputBytes(
                    property, definitions: definitions, activeReferences: activeReferences
                )
                total += keyBytes + 1 + valueBytes + (total == 2 ? 0 : 1)
                guard total <= 4_096 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            }
            return total
        }

        private func maximumScalarBytes(_ fields: [String: Any]) throws -> Int {
            if let value = fields["const"] as? String { return try serializedBytes(value) }
            if let values = fields["enum"] as? [Any] {
                let projectable = NativeChatFoundationModelsSchema.projectableEnumValues(
                    values, type: fields["type"] as? String
                )
                guard !projectable.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                return try projectable.map { try serializedBytes($0) }.max() ?? 1
            }
            if fields["type"] as? String == "number" {
                let (minimum, maximum) = try projectedNumberBounds(fields)
                // A wider interval may contain a much longer serialization than its endpoints.
                // Use a conservative Double JSON width when exhaustive enumeration is bounded out.
                return try serializedNumberWidths(from: minimum, through: maximum)?.maximum ?? 32
            }
            if fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) == "integer" {
                let (minimum, maximum) = try projectedIntegerBounds(fields)
                return try max(
                    serializedBytes(NSNumber(value: minimum)),
                    serializedBytes(NSNumber(value: maximum))
                )
            }
            return try minimumScalarBytes(fields)
        }
    }
#endif
