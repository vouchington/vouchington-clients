import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    enum NativeChatFoundationModelsToolSelection {
        static let maximumTools = 3
        static let maximumCatalogBytes = 1_600
        static let maximumPromptCharacters = 900

        struct Projected {
            let descriptor: LocalLLMFunctionTool
            let schema: GenerationSchema
        }

        private struct Ranked {
            let descriptor: LocalLLMFunctionTool
            let score: Int
        }

        static func select(_ bridge: NativeChatMCPToolBridge, matching message: String) -> [Projected] {
            let tokens = message.lowercased().split { !$0.isLetter && !$0.isNumber }.map(String.init)
            let terms = Set(tokens.filter { $0.count >= 3 })
            let shortNames = Set(tokens.filter { $0.count < 3 })
            guard !terms.isEmpty || !shortNames.isEmpty else { return [] }
            var ranked: [Ranked] = []
            for descriptor in bridge.tools {
                let name = descriptor.name.lowercased().replacingOccurrences(of: "_", with: " ")
                let description = descriptor.description.lowercased()
                var score = 0
                for term in terms {
                    if name.range(of: term) != nil { score += 3 }
                    if description.range(of: term) != nil { score += 1 }
                }
                if shortNames.contains(descriptor.name.lowercased()) { score += 3 }
                if score > 0 { ranked.append(.init(descriptor: descriptor, score: score)) }
            }
            ranked.sort { left, right in
                left.score == right.score ? left.descriptor.name < right.descriptor.name : left.score > right.score
            }

            var selected: [Projected] = []
            var remainingBytes = maximumCatalogBytes
            for item in ranked {
                let descriptor = item.descriptor
                let size = descriptor.name.utf8.count + descriptor.description.utf8.count + descriptor.parameters.count
                guard size <= remainingBytes,
                      let schema = try? NativeChatFoundationModelsSchema.project(descriptor.parameters)
                else { continue }
                selected.append(.init(descriptor: descriptor, schema: schema))
                remainingBytes -= size
                if selected.count == maximumTools { break }
            }
            return selected
        }
    }
#endif
