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
    @State
    private var promptId = ""
    @State
    private var promptText = ""
    @State
    private var promptModelName = ""
    @State
    private var promptModelProvider = ""
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
        VStack(alignment: .leading, spacing: Spacing.sm) {
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
                TextField(UiMessages.string(.nativeSwiftCommunitiesPromptId, locale: nativeUiLocale), text: $promptId)
                    .textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesPromptText, locale: nativeUiLocale),
                    text: $promptText,
                    axis: .vertical
                ).textFieldStyle(.roundedBorder)
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesModelName, locale: nativeUiLocale),
                        text: $promptModelName
                    ).textFieldStyle(.roundedBorder)
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesModelProvider, locale: nativeUiLocale),
                        text: $promptModelProvider
                    ).textFieldStyle(.roundedBorder)
                }
                HStack {
                    actionButton(.nativeSwiftCommonCreate, systemImage: "plus") {
                        Task {
                            await viewModel.createCommunityAgentPrompt(
                                prompt: promptText,
                                modelName: promptModelName,
                                modelProvider: promptModelProvider
                            )
                        }
                    }
                    actionButton(.nativeSwiftCommunityActionsUpdate, systemImage: "pencil") {
                        Task {
                            await viewModel.updateCommunityAgentPrompt(
                                promptId: promptId,
                                prompt: promptText
                            )
                        }
                    }
                    actionButton(.nativeSwiftCommunityActionsAllocate, systemImage: "person.crop.circle.badge.plus") {
                        Task { await viewModel.allocateCommunityAgentPrompt(promptId: promptId) }
                    }
                    actionButton(
                        .nativeSwiftCommunityActionsDeallocate,
                        systemImage: "person.crop.circle.badge.minus"
                    ) {
                        Task { await viewModel.deallocateCommunityAgentPrompt(promptId: promptId) }
                    }
                    actionButton(.nativeSwiftCommonDelete, systemImage: "trash") {
                        Task { await viewModel.deleteCommunityAgentPrompt(promptId: promptId) }
                    }
                }
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesTestText, locale: nativeUiLocale),
                        text: $testText,
                        axis: .vertical
                    ).textFieldStyle(.roundedBorder)
                    actionButton(.nativeSwiftCommonTest, systemImage: "testtube.2") {
                        Task {
                            await viewModel.testCommunityAgentPrompt(promptId: promptId, text: testText)
                        }
                    }
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
