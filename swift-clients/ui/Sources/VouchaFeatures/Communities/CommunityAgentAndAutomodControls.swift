import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityAgentAndAutomodControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var agentSlug = ""
    var promptId: String {
        viewModel.agentPromptWorkspace.selectedPromptId ?? ""
    }

    @State
    private var testText = ""
    @State
    private var feedbackSourceKey = ""
    @State
    private var feedbackReasonCode = ""
    @State
    private var feedbackNote = ""
    @State
    private var simulationPromptId = ""
    @State
    private var simulationPrompt = ""
    @State
    private var simulationTimeWindowHours = "24"
    @State
    private var simulationLimit = "25"
}

extension CommunityAgentAndAutomodControls {
    var body: some View {
        @Bindable var prompts = viewModel.agentPromptWorkspace
        return VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesAiAgentsAndAutomod, locale: nativeUiLocale))
                .font(Typography.headline)

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesAiAgents, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                TextField(UiMessages.string(.nativeSwiftCommunitiesAgentSlug, locale: nativeUiLocale), text: $agentSlug)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    actionButton(.nativeSwiftCommunityActionsEnable, systemImage: "power") {
                        Task { await viewModel.enableCommunityAiAgent(agentSlug: agentSlug) }
                    }
                    actionButton(.nativeSwiftCommunityActionsDisable, systemImage: "poweroff") {
                        Task { await viewModel.disableCommunityAiAgent(agentSlug: agentSlug) }
                    }
                }
            }

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesPrompts, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                CommunityAgentPromptList(viewModel: viewModel.agentPromptWorkspace)
                LabeledContent(UiMessages.string(.nativeSwiftCommunitiesPromptText, locale: nativeUiLocale)) {
                    Text(verbatim: UiMessages.string(
                        prompts.selectedPrompt.map { .verbatim($0.prompt) } ?? .message(.nativeSwiftCommunityRowsNone),
                        locale: nativeUiLocale
                    ))
                }
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesPromptText, locale: nativeUiLocale),
                    text: $prompts.draftPrompt,
                    axis: .vertical
                ).textFieldStyle(.roundedBorder)
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesModelName, locale: nativeUiLocale),
                        text: $prompts.draftModelName
                    ).textFieldStyle(.roundedBorder)
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesModelProvider, locale: nativeUiLocale),
                        text: $prompts.draftModelProvider
                    ).textFieldStyle(.roundedBorder)
                }
                promptActions
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesTestText, locale: nativeUiLocale),
                        text: $testText,
                        axis: .vertical
                    ).textFieldStyle(.roundedBorder)
                    actionButton(.nativeSwiftCommonTest, systemImage: "testtube.2") {
                        let selectedId = promptId
                        Task {
                            await viewModel.testCommunityAgentPrompt(promptId: selectedId, text: testText)
                        }
                    }
                    .disabled(isSignedIn && (prompts.selectedPrompt == nil || !viewModel.canMutateCommunityAgentPrompt))
                }
            }
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if viewModel.selectedTab != .moderation {
                    CommunityAutomodActionStatusView(action: viewModel.communityDetail?.community.automodAction)
                }
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesSourceKey, locale: nativeUiLocale),
                    text: $feedbackSourceKey
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesReasonCode, locale: nativeUiLocale),
                    text: $feedbackReasonCode
                ).textFieldStyle(.roundedBorder)
                TextField(UiMessages.string(.nativeSwiftCommunitiesNote, locale: nativeUiLocale), text: $feedbackNote)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    actionButton(.nativeSwiftCommunityActionsLoadRecent, systemImage: "clock.arrow.circlepath") {
                        Task { await viewModel.loadCommunityAutomodRecentActions() }
                    }
                    actionButton(.nativeSwiftCommunityActionsFalsePositive, systemImage: "hand.thumbsdown") {
                        Task {
                            await viewModel.recordCommunityAutomodFeedback(
                                sourceKey: feedbackSourceKey,
                                outcome: .falsePositive,
                                action: .reinstate,
                                reasonCode: feedbackReasonCode,
                                note: feedbackNote
                            )
                        }
                    }
                    actionButton(.nativeSwiftCommunityActionsTruePositive, systemImage: "hand.thumbsup") {
                        Task {
                            await viewModel.recordCommunityAutomodFeedback(
                                sourceKey: feedbackSourceKey,
                                outcome: .truePositive,
                                action: .keepRemoved,
                                reasonCode: feedbackReasonCode,
                                note: feedbackNote
                            )
                        }
                    }
                }
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesSimulationPromptId, locale: nativeUiLocale),
                    text: $simulationPromptId
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesSimulationPrompt, locale: nativeUiLocale),
                    text: $simulationPrompt,
                    axis: .vertical
                ).textFieldStyle(.roundedBorder)
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesHours, locale: nativeUiLocale),
                        text: $simulationTimeWindowHours
                    ).textFieldStyle(.roundedBorder)
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesLimit, locale: nativeUiLocale),
                        text: $simulationLimit
                    ).textFieldStyle(.roundedBorder)
                    actionButton(.nativeSwiftCommunityActionsSimulate, systemImage: "sparkles") {
                        Task {
                            await viewModel.simulateCommunityAutomod(
                                promptId: simulationPromptId,
                                prompt: simulationPrompt,
                                timeWindowHours: Int(simulationTimeWindowHours),
                                limit: Int(simulationLimit)
                            )
                        }
                    }
                }
            }
        }
    }
}
