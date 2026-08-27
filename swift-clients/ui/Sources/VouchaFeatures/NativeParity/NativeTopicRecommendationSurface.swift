import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct NativeTopicRecommendationSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: NativeTopicRecommendationViewModel
    @State
    private var showingTurnstile = false
    private let turnstileSiteKey: String?
    private let canCreateVote: Bool
    private let isForm: Bool
    private let isSignedIn: Bool
    private let showSignIn: () -> Void
    let hideDownCount: Bool

    init(
        client: APIClient?,
        recommendationId: String?,
        turnstileSiteKey: String?,
        canCreateVote: Bool = true,
        isForm: Bool = true,
        isSignedIn: Bool = true,
        showSignIn: @escaping () -> Void = {},
        hideDownCount: Bool = false
    ) {
        self.turnstileSiteKey = turnstileSiteKey
        self.canCreateVote = canCreateVote
        self.isForm = isForm
        self.isSignedIn = isSignedIn
        self.showSignIn = showSignIn
        self.hideDownCount = hideDownCount
        _viewModel = State(
            initialValue: NativeTopicRecommendationViewModel(
                client: client,
                recommendationId: recommendationId
            )
        )
    }

}

extension NativeTopicRecommendationSurface {
    var body: some View {
        @Bindable
        var viewModel = viewModel

        return VStack(alignment: .leading, spacing: Spacing.md) {
            if isForm {
                formContent(viewModel: viewModel)
                turnstileVerification(viewModel: viewModel)
                actions(viewModel: viewModel)
            } else {
                readOnlyDetailContent(viewModel: viewModel)
            }
            voteControls(viewModel: viewModel)
            status(viewModel: viewModel)
        }
        .task { await viewModel.load() }
        .emailVerificationRecovery(client: viewModel.client, gate: viewModel.emailVerificationGate)
        .sheet(isPresented: $showingTurnstile) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                viewModel.turnstileToken = token
                showingTurnstile = false
            }
        }
    }

    private func formContent(viewModel: NativeTopicRecommendationViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsType, locale: nativeUiLocale),
                selection: $viewModel.topicType
            ) {
                Text(UiMessages.string(.nativeSwiftTopicRecommendationTopic, locale: nativeUiLocale)).tag("topic")
                Text(UiMessages.string(.nativeSwiftTopicRecommendationReferral, locale: nativeUiLocale))
                    .tag("referral_program")
                Text(UiMessages.string(.nativeSwiftTopicRecommendationCard, locale: nativeUiLocale)).tag("card")
            }
            .pickerStyle(.segmented)

            TextField(
                UiMessages.string(.nativeSwiftTopicRecommendationRecommendationTitle, locale: nativeUiLocale),
                text: $viewModel.title
            )
            .textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftTopicRecommendationTopicTitle, locale: nativeUiLocale),
                text: $viewModel.topicTitle
            )
            .textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftTopicRecommendationTopicSlug, locale: nativeUiLocale),
                text: $viewModel.topicSlug
            )
            .textFieldStyle(.roundedBorder)
            TextEditor(text: $viewModel.bodyText)
                .frame(minHeight: 140)
                .overlay(editorBorder)

            topicFields(viewModel: viewModel)
        }
    }

    @ViewBuilder
    private func readOnlyDetailContent(viewModel: NativeTopicRecommendationViewModel) -> some View {
        if !viewModel.title.isEmpty {
            Text(verbatim: viewModel.title).font(Typography.headline)
        }
        if !viewModel.topicTitle.isEmpty {
            Text(verbatim: viewModel.topicTitle).font(Typography.subheadline)
        }
        if !viewModel.topicSlug.isEmpty {
            Text(verbatim: viewModel.topicSlug).font(Typography.caption).foregroundStyle(Colors.secondaryLabel)
        }
        if !viewModel.bodyText.isEmpty {
            Text(verbatim: viewModel.bodyText)
        }
    }

    @ViewBuilder
    private func voteControls(viewModel: NativeTopicRecommendationViewModel) -> some View {
        if viewModel.isEdit, let election = viewModel.postElection {
            VoteControls(
                election: election,
                myVote: viewModel.currentVote,
                policy: .recommendation,
                canCreateVote: isSignedIn && canCreateVote && !viewModel.isVoting,
                onVote: isSignedIn && !viewModel.isVoting
                    ? { choice in Task { await viewModel.vote(choice) } }
                    : nil,
                onSignedOutTap: isSignedIn ? nil : showSignIn,
                hideDownCount: hideDownCount
            )
        }
    }

    @ViewBuilder
    private func turnstileVerification(viewModel: NativeTopicRecommendationViewModel) -> some View {
        if !viewModel.isEdit {
            Button {
                showingTurnstile = true
            } label: {
                Label(
                    UiMessages.string(
                        viewModel.turnstileToken == nil
                            ? .nativeSwiftCommonVerify
                            : .nativeAuthVerified,
                        locale: nativeUiLocale
                    ),
                    systemImage: viewModel.turnstileToken == nil ? "checkmark.shield" : "checkmark.shield.fill"
                )
            }
            .buttonStyle(.bordered)
        }
    }

    private func actions(viewModel: NativeTopicRecommendationViewModel) -> some View {
        Button(UiMessages.string(
            viewModel.isEdit
                ? .nativeSwiftTopicRecommendationSaveRecommendation
                : .nativeSwiftTopicRecommendationSubmitRecommendation,
            locale: nativeUiLocale
        )) {
            Task { await viewModel.submit() }
        }
        .buttonStyle(.borderedProminent)
        .disabled(!viewModel.canSubmit || viewModel.isLoading)
    }

    @ViewBuilder
    private func status(viewModel: NativeTopicRecommendationViewModel) -> some View {
        switch viewModel.state {
        case .required:
            Text(UiMessages.string(.nativeSwiftPostComposeVerificationTokenRequired, locale: nativeUiLocale))
                .foregroundStyle(Colors.secondaryLabel)
        case let .error(error):
            Text(error.localizedDescription)
                .foregroundStyle(Colors.negativeVote)
        default:
            if let saved = viewModel.savedPostId {
                Text(UiMessages.string(
                    .nativeSwiftTopicRecommendationSavedValue,
                    parameters: ["value": UiMessages.string(.verbatim(saved), locale: nativeUiLocale)],
                    locale: nativeUiLocale
                ))
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

}
