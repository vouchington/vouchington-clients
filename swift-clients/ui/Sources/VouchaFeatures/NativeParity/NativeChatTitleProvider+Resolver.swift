import Foundation
import VouchaCore
import VouchaLocalization

struct NativeChatLiveTitleProviderResolver: NativeChatTitleProviderResolving {
    private let settingsStore: LocalLLMSettingsStore
    private let featurePolicy: LocalLLMFeaturePolicy
    private let responsesClient: OpenAICompatibleResponsesClient
    private let appleProvider: any NativeChatTitleProviding

    init(
        settingsStore: LocalLLMSettingsStore = LocalLLMSettingsStore(),
        featurePolicy: LocalLLMFeaturePolicy = LocalLLMFeaturePolicy(),
        responsesClient: OpenAICompatibleResponsesClient = OpenAICompatibleResponsesClient(),
        appleProvider: (any NativeChatTitleProviding)? = nil
    ) {
        self.settingsStore = settingsStore
        self.featurePolicy = featurePolicy
        self.responsesClient = responsesClient
        self.appleProvider = appleProvider ?? NativeChatAppleFoundationModelsProvider(featurePolicy: featurePolicy)
    }

    func defaultSelection() -> NativeChatTitleProviderKind {
        let configuration = settingsStore.load()
        if let selectedProviderID = configuration.selectedProviderID {
            return NativeChatTitleProviderKind(persistedID: selectedProviderID)
        }
        if appleProvider.status.isAvailable {
            return .appleFoundationModels
        }
        if let endpoint = configuration.endpoints.first(where: { endpoint in
            NativeChatLocalTitleProvider(
                endpointID: endpoint.id,
                settingsStore: settingsStore,
                featurePolicy: featurePolicy,
                responsesClient: responsesClient
            ).status.isAvailable
        }) {
            return .openAICompatible(endpointID: endpoint.id)
        }
        return .appleFoundationModels
    }

    func provider(for kind: NativeChatTitleProviderKind) -> any NativeChatTitleProviding {
        switch kind {
        case .appleFoundationModels:
            appleProvider
        case let .openAICompatible(endpointID):
            NativeChatLocalTitleProvider(
                endpointID: endpointID,
                settingsStore: settingsStore,
                featurePolicy: featurePolicy,
                responsesClient: responsesClient
            )
        case .androidAICore:
            NativeChatAndroidAICoreProvider()
        case let .unavailable(id):
            NativeChatUnavailableTitleProvider(id: id)
        }
    }

    /// `.androidAICore` is intentionally absent from the base list built here: it is never
    /// reachable on any platform `ui/` builds today (see `NativeChatTitleProvider+AndroidAICore.swift`),
    /// so offering it in the picker would only ever surface a permanently unavailable row. A
    /// persisted `"android_aicore"` selection still round-trips safely through the fallback branch
    /// below, which appends whatever kind is currently selected even if it isn't in the base list.
    func providerDescriptors() -> [NativeChatProviderDescriptor] {
        let configuration = settingsStore.load()
        var descriptors = [descriptor(for: .appleFoundationModels)]
        descriptors += configuration.endpoints.map { endpoint in
            descriptor(for: .openAICompatible(endpointID: endpoint.id), endpoint: endpoint)
        }

        if let selectedProviderID = configuration.selectedProviderID,
           descriptors.contains(where: { $0.id == selectedProviderID }) == false {
            descriptors.append(descriptor(for: .init(persistedID: selectedProviderID)))
        }
        return descriptors
    }

    func persistSelection(_ kind: NativeChatTitleProviderKind) async -> Bool {
        let selectedProviderID = kind.id
        let selectedEndpointID: UUID? = if case let .openAICompatible(endpointID) = kind {
            endpointID
        } else {
            nil
        }
        return await settingsStore.update { configuration in
            configuration.selectedProviderID = selectedProviderID
            if let selectedEndpointID {
                configuration.selectedEndpointID = selectedEndpointID
            }
        }
    }

    private func descriptor(
        for kind: NativeChatTitleProviderKind,
        endpoint: LocalLLMEndpointProfile? = nil
    ) -> NativeChatProviderDescriptor {
        let provider = provider(for: kind)
        let displayName: UiVerbatimText
        if let endpoint {
            let name = endpoint.displayName.trimmingCharacters(in: .whitespacesAndNewlines)
            displayName = name.isEmpty
                ? .verbatim(endpoint.endpoint)
                : .verbatim(name)
        } else {
            displayName = kind.displayName
        }
        return NativeChatProviderDescriptor(
            selection: kind,
            displayName: displayName,
            status: provider.status
        )
    }
}
