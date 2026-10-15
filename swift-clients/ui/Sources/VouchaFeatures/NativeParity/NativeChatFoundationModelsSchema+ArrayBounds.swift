import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        mutating func projectArray(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(
                fields, allowing: ["type", "items", "minItems", "maxItems", "uniqueItems", "$defs"]
            )
            guard fields["items"] == nil || fields["items"] is [String: Any] else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            let item = fields["items"] as? [String: Any] ?? [:]
            let minimum = fields["minItems"] as? Int ?? 0
            guard (0 ... 64).contains(minimum),
                  (fields["maxItems"] as? Int ?? minimum) >= minimum else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            let maximum = NativeChatFoundationModelsSchema.projectedArrayMaximum(fields, definitions: definitions)
            if fields["uniqueItems"] as? Bool == true,
               NativeChatFoundationModelsSchema.singletonItemDomain(item, definitions: definitions) != nil,
               minimum > 1 { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            guard fields["uniqueItems"] == nil || fields["uniqueItems"] as? Bool == false
                || (fields["uniqueItems"] as? Bool == true && maximum <= 1) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return try .init(
                arrayOf: project(maximum == 0 ? [:] : item),
                minimumElements: fields["minItems"] as? Int,
                maximumElements: maximum
            )
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func projectedArrayMaximum(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) -> Int {
            let maximum = min(fields["maxItems"] as? Int ?? 64, 64)
            if fields["uniqueItems"] as? Bool == true,
               let item = fields["items"] as? [String: Any],
               singletonItemDomain(item, definitions: definitions) != nil {
                return min(maximum, 1)
            }
            return maximum
        }

        static func singletonItemDomain(
            _ item: [String: Any], definitions: [String: [String: Any]], activeReferences: Set<String> = []
        ) -> String? {
            if let path = item["$ref"] as? String {
                guard path.hasPrefix("#/$defs/") else { return nil }
                let key = String(path.dropFirst(8))
                guard let target = definitions[key], !activeReferences.contains(key) else { return nil }
                return singletonItemDomain(
                    target, definitions: definitions, activeReferences: activeReferences.union([key])
                )
            }
            if let choices = item["anyOf"] as? [[String: Any]], !choices.isEmpty {
                return singletonChoiceDomain(
                    choices, definitions: definitions, activeReferences: activeReferences
                )
            }
            if let constant = item["const"] as? String { return "string:\(constant)" }
            if item["const"] is NSNull { return "null" }
            if let constant = item["const"], let number = try? safeNumberConstant(constant) {
                return "number:\(number)"
            }
            guard let candidates = item["enum"] as? [Any] else { return nil }
            let type = item["type"] as? String
            let values = projectableEnumValues(candidates, type: type)
            guard !values.isEmpty else { return nil }
            if values.allSatisfy({ $0 is String }) {
                let strings = Set(values.compactMap { $0 as? String })
                return strings.count == 1 ? "string:\(strings.first ?? "")" : nil
            }
            return values.allSatisfy { $0 is NSNull } ? "null" : nil
        }

        private static func singletonChoiceDomain(
            _ choices: [[String: Any]], definitions: [String: [String: Any]], activeReferences: Set<String>
        ) -> String? {
            let domains = choices.compactMap {
                singletonItemDomain($0, definitions: definitions, activeReferences: activeReferences)
            }
            guard domains.count == choices.count, Set(domains).count == 1 else { return nil }
            return domains[0]
        }
    }
#endif
