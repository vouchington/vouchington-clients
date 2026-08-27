import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct MemberAppealForm: View {
    @Environment(\.locale) private var locale
    let viewModel: MemberAppealsViewModel
    let turnstileSiteKey: String?

    var body: some View {
        ZStack {
            NavigationStack {
                Form {
                    Picker(
                        UiMessages.string(.nativeSwiftModerationAppealsMemberReason, locale: locale),
                        selection: reasonBinding
                    ) {
                        Text(UiMessages.string(
                            .nativeSwiftModerationAppealsMemberSelectReason,
                            locale: locale
                        )).tag(ModerationAppealReason?.none)
                        ForEach(ModerationAppealReason.allCases, id: \.rawValue) { reason in
                            Text(UiMessages.string(reason.memberTitleKey, locale: locale))
                                .tag(Optional(reason))
                        }
                    }
                    TextEditor(text: detailsBinding)
                        .frame(minHeight: 120)
                        .accessibilityLabel(UiMessages.string(
                            .nativeSwiftModerationAppealsMemberDetails,
                            locale: locale
                        ))
                    if let message = viewModel.submissionMessage {
                        Text(UiMessages.string(message, locale: locale))
                            .foregroundStyle(messageColor)
                            .accessibilityIdentifier("member-appeal-submission-message")
                    }
                    Button(verificationTitle) { viewModel.isPresentingTurnstile = true }
                    Button(UiMessages.string(
                        .nativeSwiftModerationAppealsMemberSubmit,
                        locale: locale
                    )) {
                        Task { await viewModel.submitAppeal() }
                    }
                    .disabled(viewModel.isSubmitting || viewModel.turnstileToken == nil)
                }
                .navigationTitle(UiMessages.string(
                    .nativeSwiftModerationAppealsMemberFileAppeal,
                    locale: locale
                ))
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button(UiMessages.string(
                            .nativeSwiftModerationAppealsMemberCancel,
                            locale: locale
                        )) { viewModel.cancelAppeal() }
                    }
                }
            }

            if viewModel.isPresentingTurnstile {
                Color.black.opacity(0.35).ignoresSafeArea()
                VStack {
                    NativeTurnstileChallengeView(
                        siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
                    ) { token in
                        viewModel.turnstileToken = token
                        viewModel.isPresentingTurnstile = false
                    }
                    Button(UiMessages.string(
                        .nativeSwiftModerationAppealsMemberCancel,
                        locale: locale
                    )) {
                        viewModel.isPresentingTurnstile = false
                    }
                }
                .padding(Spacing.md)
                .background(.regularMaterial)
                .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
                .padding(Spacing.md)
            }
        }
    }

    private var reasonBinding: Binding<ModerationAppealReason?> {
        Binding(get: { viewModel.activeDraft.reason }, set: viewModel.setReason)
    }

    private var detailsBinding: Binding<String> {
        Binding(get: { viewModel.activeDraft.details }, set: viewModel.setDetails)
    }

    private var verificationTitle: String {
        UiMessages.string(
            viewModel.turnstileToken == nil
                ? .nativeSwiftModerationAppealsMemberVerify
                : .nativeSwiftModerationAppealsMemberVerified,
            locale: locale
        )
    }

    private var messageColor: Color {
        switch viewModel.submissionState {
        case .succeeded: .green
        case .failed: .red
        default: .secondary
        }
    }
}

extension ModerationAppealReason {
    var memberTitleKey: UiMessageKey {
        switch self {
        case .incorrectFacts: .nativeSwiftModerationAppealsMemberReasonIncorrectFacts
        case .wrongRule: .nativeSwiftModerationAppealsMemberReasonWrongRule
        case .contextMissing: .nativeSwiftModerationAppealsMemberReasonContextMissing
        case .disproportionate: .nativeSwiftModerationAppealsMemberReasonDisproportionate
        case .other: .nativeSwiftModerationAppealsMemberReasonOther
        }
    }
}
