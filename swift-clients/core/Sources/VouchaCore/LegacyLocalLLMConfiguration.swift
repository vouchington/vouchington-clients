import Foundation

struct LegacyLocalLLMConfiguration: Decodable {
    let isEnabled: Bool
    let endpoint: String
    let modelNames: [String]
    let selectedModelName: String

    func migratedConfiguration(endpointID: UUID) -> LocalLLMConfiguration {
        LocalLLMConfiguration(
            isEnabled: isEnabled,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: endpointID,
                    isEnabled: isEnabled,
                    endpoint: endpoint,
                    modelNames: modelNames,
                    selectedModelName: selectedModelName
                )
            ],
            selectedEndpointID: endpointID,
            selectedProviderID: isEnabled
                ? "openai_compatible:\(endpointID.uuidString.lowercased())"
                : nil
        )
    }
}
