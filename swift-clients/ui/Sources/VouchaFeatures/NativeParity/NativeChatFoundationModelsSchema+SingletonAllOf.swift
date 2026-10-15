import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        /// Normalize only assertion-free one-branch wrappers before every projection preflight.
        static func normalizeSingletonAllOf(
            _ fields: [String: Any], strict: Bool = true
        ) throws -> [String: Any] {
            if let allOf = fields["allOf"] {
                let hasOnlyWrapperAssertions = (try? validateKeywords(fields, allowing: ["allOf", "$defs"])) != nil
                guard hasOnlyWrapperAssertions,
                      let branches = allOf as? [[String: Any]], branches.count == 1 else {
                    if !strict { return fields }
                    throw Failure.unsupported
                }
                var branch = branches[0]
                if let definitions = fields["$defs"] {
                    guard branch["$defs"] == nil else {
                        if !strict { return fields }
                        throw Failure.unsupported
                    }
                    branch["$defs"] = definitions
                }
                if branch["description"] == nil { branch["description"] = fields["description"] }
                return try normalizeSingletonAllOf(branch, strict: strict)
            }
            return try normalizeChildren(fields, strict: strict)
        }

        private static func normalizeChildren(
            _ fields: [String: Any], strict: Bool
        ) throws -> [String: Any] {
            var normalized = fields
            if fields["maxProperties"] as? Int != 0,
               let properties = fields["properties"] as? [String: [String: Any]] {
                normalized["properties"] = try properties.mapValues { try normalizeSingletonAllOf($0, strict: strict) }
            }
            if fields["maxItems"] as? Int != 0, let items = fields["items"] as? [String: Any] {
                normalized["items"] = try normalizeSingletonAllOf(items, strict: strict)
            }
            for key in ["anyOf", "oneOf"] {
                if let branches = fields[key] as? [[String: Any]] {
                    normalized[key] = try branches.map { try normalizeSingletonAllOf($0, strict: strict) }
                }
            }
            if let definitions = fields["$defs"] as? [String: [String: Any]] {
                normalized["$defs"] = try definitions.mapValues { try normalizeSingletonAllOf($0, strict: false) }
            }
            return normalized
        }
    }
#endif
