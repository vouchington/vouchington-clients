import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        /// Mirrors the validator's bounded recursive checks for a minimally required result.
        /// Every projected child is traversed for depth, including optional fields.
        func validateProjectedWork(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws {
            _ = try minimumValidationWork(fields, definitions: definitions, activeReferences: [], depth: 0)
        }

        private func minimumValidationWork(
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
                        work = try addWork(work, minimumValidationWork(
                            branch, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                        ))
                    }
                }
            }
            if let properties = fields["properties"] as? [String: [String: Any]] {
                let required = Set(fields["required"] as? [String] ?? [])
                for (key, property) in properties {
                    let child = try minimumValidationWork(
                        property, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                    )
                    if required.contains(key) { work = try addWork(work, child) }
                }
            }
            if let item = fields["items"] as? [String: Any] {
                let child = try minimumValidationWork(
                    item, definitions: definitions, activeReferences: activeReferences, depth: depth + 1
                )
                let minimum = fields["minItems"] as? Int ?? 0
                guard (0 ... 64).contains(minimum), minimum == 0 || child <= 4_096 / minimum else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                work = try addWork(work, minimum * child)
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
            return try minimumValidationWork(
                target, definitions: definitions, activeReferences: activeReferences.union([key]), depth: depth + 1
            )
        }

        private func addWork(_ current: Int, _ next: Int) throws -> Int {
            guard next <= 4_096 - current else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return current + next
        }
    }
#endif
