import SwiftUI
import VouchaLocalization

extension CommunityAgentAndAutomodControls {
    func actionButton(
        _ title: UiMessageKey,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage) {
            guard isSignedIn else {
                showSignIn()
                return
            }
            action()
        }
    }

    var promptActions: some View {
        HStack {
            actionButton(.nativeSwiftCommonCreate, systemImage: "plus") {
                Task {
                    await viewModel.createCommunityAgentPrompt(
                        prompt: viewModel.agentPromptWorkspace.draftPrompt,
                        modelName: viewModel.agentPromptWorkspace.draftModelName,
                        modelProvider: viewModel.agentPromptWorkspace.draftModelProvider
                    )
                }
            }
            HStack {
                actionButton(.nativeSwiftCommunityActionsUpdate, systemImage: "pencil") {
                    let selectedId = promptId
                    Task {
                        await viewModel.updateCommunityAgentPrompt(
                            promptId: selectedId,
                            prompt: viewModel.agentPromptWorkspace.draftPrompt
                        )
                    }
                }
                actionButton(.nativeSwiftCommunityActionsAllocate, systemImage: "person.crop.circle.badge.plus") {
                    let selectedId = promptId
                    Task { await viewModel.allocateCommunityAgentPrompt(promptId: selectedId) }
                }
                actionButton(
                    .nativeSwiftCommunityActionsDeallocate,
                    systemImage: "person.crop.circle.badge.minus"
                ) {
                    let selectedId = promptId
                    Task { await viewModel.deallocateCommunityAgentPrompt(promptId: selectedId) }
                }
                actionButton(.nativeSwiftCommonDelete, systemImage: "trash") {
                    let selectedId = promptId
                    Task { await viewModel.deleteCommunityAgentPrompt(promptId: selectedId) }
                }
            }
            .disabled(isSignedIn && viewModel.agentPromptWorkspace.selectedPrompt == nil)
        }
        .disabled(isSignedIn && !viewModel.canMutateCommunityAgentPrompt)
    }

}
