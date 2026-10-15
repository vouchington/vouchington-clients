import Foundation

#if canImport(FoundationModels)
    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        private func branchFits(_ branchType: String, parentType: String) -> Bool {
            branchType == parentType || (parentType == "number" && branchType == "integer")
        }

        func validateChoiceSiblingTypes(_ choices: [[String: Any]], fields: [String: Any]) throws {
            if let type = fields["type"] as? String {
                guard choices.allSatisfy({ branch in
                    guard let branchType = NativeChatFoundationModelsSchema.branchType(branch) else { return false }
                    return branchFits(branchType, parentType: type)
                }) else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            }
            if let types = fields["type"] as? [String] {
                guard choices.allSatisfy({ branch in
                    guard let type = NativeChatFoundationModelsSchema.branchType(branch) else { return false }
                    return types.contains { branchFits(type, parentType: $0) }
                }) else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            }
        }
    }
#endif
