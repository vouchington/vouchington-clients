import Foundation
import VouchaCore

public struct LocalLLMEndpointDraft: Sendable, Equatable {
    public var displayName: String
    public var isEnabled: Bool
    public var endpoint: String
    public var modelsText: String
    public var selectedModel: String
    public var apiKey: String

    public init(
        displayName: String,
        isEnabled: Bool,
        endpoint: String,
        modelsText: String,
        selectedModel: String,
        apiKey: String
    ) {
        self.displayName = displayName
        self.isEnabled = isEnabled
        self.endpoint = endpoint
        self.modelsText = modelsText
        self.selectedModel = selectedModel
        self.apiKey = apiKey
    }

    init(profile: LocalLLMEndpointProfile) {
        self.init(
            displayName: profile.displayName,
            isEnabled: profile.isEnabled,
            endpoint: profile.endpoint,
            modelsText: profile.normalizedModelNames.joined(separator: "\n"),
            selectedModel: profile.selectedModelName,
            apiKey: ""
        )
    }

    func matchesExceptAPIKey(_ other: LocalLLMEndpointDraft) -> Bool {
        displayName == other.displayName
            && isEnabled == other.isEnabled
            && endpoint == other.endpoint
            && modelsText == other.modelsText
            && selectedModel == other.selectedModel
    }
}
