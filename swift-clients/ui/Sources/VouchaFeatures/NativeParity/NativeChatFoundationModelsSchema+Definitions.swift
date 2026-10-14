import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func inferredType(_ fields: [String: Any]) -> String {
            if fields["properties"] != nil { return "object" }
            if fields["items"] != nil { return "array" }
            if fields["const"] is String { return "string" }
            return ""
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        func reachableDefinitions(
            in root: [String: Any], definitions: [String: [String: Any]]
        ) throws -> Set<String> {
            var pending: [String] = []
            try collectReferences(in: root, into: &pending, depth: 0)
            var reachable: Set<String> = []
            while let name = pending.popLast() {
                guard let definition = definitions[name] else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                guard reachable.insert(name).inserted else { continue }
                try collectReferences(in: definition, into: &pending, depth: 0)
            }
            return reachable
        }

        private func collectReferences(in value: Any, into pending: inout [String], depth: Int) throws {
            guard depth <= 32 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if let fields = value as? [String: Any] {
                if let path = fields["$ref"] as? String {
                    guard path.hasPrefix("#/$defs/"), path.count > 8 else {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
                    pending.append(String(path.dropFirst(8)))
                }
                if let properties = fields["properties"] as? [String: Any] {
                    for child in properties.values {
                        try collectReferences(in: child, into: &pending, depth: depth + 1)
                    }
                }
                if let items = fields["items"] {
                    try collectReferences(in: items, into: &pending, depth: depth + 1)
                }
                for key in ["anyOf", "oneOf"] {
                    if let choices = fields[key] as? [Any] {
                        for child in choices {
                            try collectReferences(in: child, into: &pending, depth: depth + 1)
                        }
                    }
                }
            }
        }
    }
#endif
