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
            let maximum = NativeChatFoundationModelsSchema.projectedArrayMaximum(fields)
            if fields["uniqueItems"] as? Bool == true, NativeChatFoundationModelsSchema.singletonItemDomain(item),
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
        static func projectedArrayMaximum(_ fields: [String: Any]) -> Int {
            let maximum = min(fields["maxItems"] as? Int ?? 64, 64)
            if fields["uniqueItems"] as? Bool == true,
               let item = fields["items"] as? [String: Any], singletonItemDomain(item) {
                return min(maximum, 1)
            }
            return maximum
        }

        static func singletonItemDomain(_ item: [String: Any]) -> Bool {
            if item["const"] != nil { return true }
            guard let candidates = item["enum"] as? [Any] else { return false }
            let type = item["type"] as? String
            let values = projectableEnumValues(candidates, type: type)
            guard !values.isEmpty else { return false }
            if values.allSatisfy({ $0 is String }) {
                return Set(values.compactMap { $0 as? String }).count == 1
            }
            return values.allSatisfy { $0 is NSNull }
        }
    }
#endif
