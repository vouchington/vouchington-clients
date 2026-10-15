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
            let maximum = min(fields["maxItems"] as? Int ?? 64, 64)
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
#endif
