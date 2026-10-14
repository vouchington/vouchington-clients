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
            guard let leftType = left["type"] as? String, let rightType = right["type"] as? String else {
                return false
            }
            if leftType != rightType {
                return Set([leftType, rightType]) != Set(["integer", "number"])
            }
            if leftType == "string", let leftValues = stringDomain(left), let rightValues = stringDomain(right) {
                return leftValues.isDisjoint(with: rightValues)
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

        private static func stringDomain(_ fields: [String: Any]) -> Set<String>? {
            if let value = fields["const"] as? String { return [value] }
            if let values = fields["enum"] as? [String], !values.isEmpty { return Set(values) }
            return nil
        }
    }
#endif
