import Foundation
import VouchaCore
import VouchaLocalization

public extension SettingsViewModel {
    func testLocalLLMSettings() async {
        let configuration = buildLocalLLMConfiguration(defaultToFirstModel: false)
        await runLocalLLMTest(configuration: configuration, apiKey: localLLMAPIKey)
    }

    func testLocalLLMEndpoint(draft: LocalLLMEndpointDraft) async {
        let profile = LocalLLMEndpointProfile(
            id: UUID(),
            displayName: draft.displayName.trimmingCharacters(in: .whitespacesAndNewlines),
            isEnabled: true,
            endpoint: draft.endpoint,
            modelNames: normalizedModelNames(from: draft.modelsText),
            selectedModelName: draft.selectedModel.trimmingCharacters(in: .whitespacesAndNewlines)
        )
        let configuration = LocalLLMConfiguration(isEnabled: true, endpoints: [profile], selectedEndpointID: profile.id)
        await runLocalLLMTest(configuration: configuration, apiKey: draft.apiKey)
    }

    private func runLocalLLMTest(configuration: LocalLLMConfiguration, apiKey: String) async {
        do {
            let response = try await localLLMResponsesClient.generateAssistantResponse(
                message: "Reply with OK.",
                history: [],
                configuration: configuration,
                apiKey: apiKey
            )
            localLLMStatusMessage = response?.isEmpty == false
                ? .message(.nativeSwiftSettingsLocalModelTestPassed)
                : .message(.nativeSwiftSettingsLocalModelEmptyResponse)
        } catch let error as LocalLLMError {
            localLLMStatusMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            localLLMStatusMessage = .verbatim(error.localizedDescription)
        }
    }
}
