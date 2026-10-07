import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityActionRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        HStack {
            if viewModel.summary.isMember {
                Button(UiMessages.string(.nativeSwiftCommunitiesLeave, locale: nativeUiLocale)) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.leave() }
                }
            } else if viewModel.summary.canJoin {
                Button(UiMessages.string(.nativeSwiftCommunitiesJoin, locale: nativeUiLocale)) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.join() }
                }
            }
            archiveButton
        }
        .buttonStyle(.bordered)
    }

    @ViewBuilder
    private var archiveButton: some View {
        if viewModel.summary.isOwner {
            Button(UiMessages.string(
                viewModel.summary.isArchived ? .nativeSwiftCommonUnarchive : .nativeSwiftCommonArchive,
                locale: nativeUiLocale
            )) {
                guard isSignedIn else {
                    showSignIn()
                    return
                }
                Task {
                    if viewModel.summary.isArchived {
                        await viewModel.unarchive()
                    } else {
                        await viewModel.archive()
                    }
                }
            }
        }
    }
}

struct CommunityTabControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if viewModel.selectedTab == .applications {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(viewModel.applicationQuestions) { question in
                    TextField(
                        UiMessages.string(question.label, locale: nativeUiLocale),
                        text: answerBinding(for: question)
                    )
                    .textFieldStyle(.roundedBorder)
                }
                HStack {
                    TextField(
                        UiMessages.string(.nativeSwiftCommunitiesApplicationMessage, locale: nativeUiLocale),
                        text: $viewModel.applicationMessage
                    )
                    .textFieldStyle(.roundedBorder)
                    Button(UiMessages.string(.nativeSwiftCommonApply, locale: nativeUiLocale)) {
                        guard isSignedIn else {
                            showSignIn()
                            return
                        }
                        Task { await viewModel.apply() }
                    }
                }
            }
        } else if viewModel.selectedTab == .invites {
            HStack {
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesEmailOrUsername, locale: nativeUiLocale),
                    text: $viewModel.inviteRecipient
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommunitiesInvite, locale: nativeUiLocale)) {
                    Task { await viewModel.invite() }
                }
            }
        }
    }

    private func answerBinding(for question: CommunityApplicationQuestion) -> Binding<String> {
        Binding {
            viewModel.applicationAnswers[question.id]?.bindingText ?? ""
        } set: { value in
            viewModel.applicationAnswers[question.id] = .string(value)
        }
    }
}

private extension CommunityApplicationQuestion {
    var label: UiVerbatimText {
        isRequired
            ? .message(
                .nativeSwiftPresentationValuesRequiredQuestion,
                textParameters: ["question": .verbatim(question)]
            )
            : .verbatim(question)
    }
}
