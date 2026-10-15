import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        /// Bounds validator work for the largest projected result, including optional fields.
        func validateProjectedWork(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws {
            _ = try projectedValidationWork(fields, definitions: definitions, activeReferences: [], depth: 0)
        }

        private func projectedValidationWork(
            _ fields: [String: Any], definitions: [String: [String: Any]],
            activeReferences: Set<String>, depth: Int
        ) throws -> Int {
            guard depth < 32 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            var work = 1
            if let path = fields["$ref"] as? String {
                work = try addWork(work, referencedWork(
                    path, definitions: definitions, activeReferences: activeReferences, depth: depth
                ))
            }
            for choice in ["anyOf", "oneOf"] {
                if let branches = fields[choice] as? [[String: Any]] {
                    for branch in branches {
                        work = try addWork(work, projectedValidationWork(
                            branch, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                        ))
                    }
                }
            }
            let projectsObject = fields["type"] as? String == "object"
                || (fields["type"] == nil && NativeChatFoundationModelsSchema.inferredType(fields) == "object")
            if projectsObject, fields["maxProperties"] as? Int != 0 {
                for property in projectedObjectProperties(fields).values {
                    let child = try projectedValidationWork(
                        property, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                    )
                    work = try addWork(work, child)
                }
            }
            return try addWork(work, projectedArrayWork(
                fields, definitions: definitions, activeReferences: activeReferences, depth: depth
            ))
        }

        private func projectedArrayWork(
            _ fields: [String: Any], definitions: [String: [String: Any]],
            activeReferences: Set<String>, depth: Int
        ) throws -> Int {
            if let type = fields["type"] as? String, type != "array" { return 0 }
            if let types = fields["type"] as? [String], !types.contains("array") { return 0 }
            guard let item = fields["items"] as? [String: Any] else { return 0 }
            let maximum = min(fields["maxItems"] as? Int ?? 64, 64)
            guard maximum >= 0 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if maximum == 0 { return 0 }
            let child = try projectedValidationWork(
                item, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
            )
            guard child <= 4_096 / maximum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return maximum * child
        }

        private func referencedWork(
            _ path: String, definitions: [String: [String: Any]],
            activeReferences: Set<String>, depth: Int
        ) throws -> Int {
            guard path.hasPrefix("#/$defs/") else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            let key = String(path.dropFirst(8))
            guard let target = definitions[key], !activeReferences.contains(key) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return try projectedValidationWork(
                target, definitions: definitions, activeReferences: activeReferences.union([key]), depth: depth + 1
            )
        }

        private func addWork(_ current: Int, _ next: Int) throws -> Int {
            guard next <= 4_096 - current else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return current + next
        }
    }
#endif
