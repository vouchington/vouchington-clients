import Foundation
import VouchaModels

extension SourceCreationViewModel {
    func resolveDestination(_ result: ImportResult) async throws -> String? {
        if let path = Self.sourcePath(result.topicSlug) { return path }
        guard let id = result.entityId, !id.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        let topic = try await service.topic(id)
        return Self.sourcePath(topic)
    }

    private static func sourcePath(_ value: String?) -> String? {
        guard let value, !value.isEmpty, value != ".", value != "..",
              value.rangeOfCharacter(from: .whitespacesAndNewlines.union(.controlCharacters)) == nil,
              value.rangeOfCharacter(from: CharacterSet(charactersIn: "/\\?#%")) == nil,
              let segment = value.addingPercentEncoding(withAllowedCharacters:
                  .urlPathAllowed.subtracting(CharacterSet(charactersIn: "/;?%#\\"))) else { return nil }
        return "/source/\(segment)"
    }

}
