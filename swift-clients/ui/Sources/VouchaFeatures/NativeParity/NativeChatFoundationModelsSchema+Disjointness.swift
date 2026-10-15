import Foundation

#if canImport(FoundationModels)
    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        /// FoundationModels exposes an any-of projection. Only use it for one-of when
        /// the advertised branches cannot accept the same JSON value.
        static func branchesAreDisjoint(_ branches: [[String: Any]]) -> Bool {
            for index in branches.indices {
                for prior in branches.indices where prior < index {
                    if !branchesAreDisjoint(branches[index], branches[prior]) { return false }
                }
            }
            return true
        }

        private static func branchesAreDisjoint(_ left: [String: Any], _ right: [String: Any]) -> Bool {
            guard let leftType = branchType(left), let rightType = branchType(right) else {
                return false
            }
            if leftType != rightType {
                guard Set([leftType, rightType]) == Set(["integer", "number"]) else { return true }
                guard let leftConstant = left["const"], let rightConstant = right["const"],
                      let leftValue = numericConstant(leftConstant, type: leftType),
                      let rightValue = numericConstant(rightConstant, type: rightType) else { return false }
                return leftValue != rightValue
            }
            if leftType == "string", let leftValues = stringDomain(left), let rightValues = stringDomain(right) {
                return leftValues.isDisjoint(with: rightValues)
            }
            if ["integer", "number"].contains(leftType),
               let leftConstant = left["const"], let rightConstant = right["const"],
               let leftValue = numericConstant(leftConstant, type: leftType),
               let rightValue = numericConstant(rightConstant, type: rightType) {
                return leftValue != rightValue
            }
            guard leftType == "object",
                  let leftProperties = left["properties"] as? [String: [String: Any]],
                  let rightProperties = right["properties"] as? [String: [String: Any]] else { return false }
            let commonRequired = Set(left["required"] as? [String] ?? [])
                .intersection(right["required"] as? [String] ?? [])
            return commonRequired.contains { key in
                guard let leftProperty = leftProperties[key], let rightProperty = rightProperties[key],
                      let leftValues = stringDomain(leftProperty), let rightValues = stringDomain(rightProperty)
                else { return false }
                return leftValues.isDisjoint(with: rightValues)
            }
        }

        static func branchType(_ fields: [String: Any]) -> String? {
            if let type = fields["type"] as? String { return type }
            if fields["const"] is NSNull { return "null" }
            let inferred = inferredType(fields)
            return inferred.isEmpty ? nil : inferred
        }

        private static func stringDomain(_ fields: [String: Any]) -> Set<String>? {
            if let value = fields["const"] as? String { return [value] }
            if let values = fields["enum"] as? [String], !values.isEmpty { return Set(values) }
            return nil
        }

        private static func numericConstant(_ constant: Any, type: String) -> Decimal? {
            try? (type == "number" ? safeNumberConstant(constant) : safeIntegerConstant(constant))
        }
    }
#endif
