import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func validateKeywords(_ fields: [String: Any], allowing assertions: Set<String>) throws {
            let annotations: Set = [
                "title",
                "description",
                "default",
                "examples",
                "deprecated",
                "readOnly",
                "writeOnly"
            ]
            let declaredTypes: Set<String> = if let type = fields["type"] as? String {
                [type]
            } else if let types = fields["type"] as? [String] {
                Set(types)
            } else {
                []
            }
            var allowed = assertions.union(annotations)
            if !declaredTypes.isEmpty {
                if !declaredTypes.contains("string") {
                    allowed.formUnion(["minLength", "maxLength", "pattern", "format"])
                }
                if declaredTypes.isDisjoint(with: ["integer", "number"]) {
                    allowed.formUnion(["minimum", "maximum"])
                }
                if !declaredTypes.contains("array") {
                    allowed.formUnion(["items", "minItems", "maxItems", "uniqueItems"])
                }
                if !declaredTypes.contains("object") {
                    allowed.formUnion([
                        "properties",
                        "required",
                        "additionalProperties",
                        "minProperties",
                        "maxProperties"
                    ])
                }
            }
            guard Set(fields.keys).isSubset(of: allowed) else {
                throw Failure.unsupported
            }
        }
    }
#endif
