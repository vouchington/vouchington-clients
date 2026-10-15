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
            if let properties = fields["properties"] as? [String: [String: Any]] {
                for property in properties.values {
                    let child = try projectedValidationWork(
                        property, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                    )
                    work = try addWork(work, child)
                }
            }
            if let item = fields["items"] as? [String: Any] {
                let child = try projectedValidationWork(
                    item, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                )
                let maximum = min(fields["maxItems"] as? Int ?? 64, 64)
                guard maximum >= 0, maximum == 0 || child <= 4_096 / maximum else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                work = try addWork(work, maximum * child)
            }
            return work
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
