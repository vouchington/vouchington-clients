import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct NativeCommentThreadComposerState: Identifiable {
    enum Kind: Hashable {
        case reply(parentId: String)
        case edit(postId: String)
        case report(postId: String)
        case delete(postId: String)
    }

    let id = UUID()
    let kind: Kind
    var markdown: String
    var reason: String = "spam"
    var note: String = ""
    var turnstileToken: String?
}

struct NativeCommentThreadComposerSheet: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var composer: NativeCommentThreadComposerState
    @Binding
    var showingTurnstile: Bool
    let client: APIClient?
    let turnstileSiteKey: String?
    let onSubmit: (NativeCommentThreadComposerState) async -> Void
    let onCancel: () -> Void
    let onCaptureTurnstile: (String) -> Void

    init(
        composer: NativeCommentThreadComposerState,
        showingTurnstile: Binding<Bool>,
        client: APIClient? = nil,
        turnstileSiteKey: String?,
        onSubmit: @escaping (NativeCommentThreadComposerState) async -> Void,
        onCancel: @escaping () -> Void,
        onCaptureTurnstile: @escaping (String) -> Void
    ) {
        _composer = State(initialValue: composer)
        _showingTurnstile = showingTurnstile
        self.client = client
        self.turnstileSiteKey = turnstileSiteKey
        self.onSubmit = onSubmit
        self.onCancel = onCancel
        self.onCaptureTurnstile = onCaptureTurnstile
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Text(UiMessages.string(title, locale: nativeUiLocale))
                .font(Typography.headline)

            if case .report = composer.kind {
                Picker(
                    UiMessages.string(.nativeSwiftCommunitiesReason, locale: nativeUiLocale),
                    selection: $composer.reason
                ) {
                    Text(UiMessages.string(.nativeSwiftCommentThreadSpam, locale: nativeUiLocale)).tag("spam")
                    Text(UiMessages.string(.nativeSwiftCommentThreadMisinformation, locale: nativeUiLocale))
                        .tag("misinformation")
                    Text(UiMessages.string(.nativeSwiftCommentThreadHarassment, locale: nativeUiLocale))
                        .tag("harassment")
                    Text(UiMessages.string(.nativeSwiftCommentThreadVoteManipulation, locale: nativeUiLocale))
                        .tag("vote_manipulation")
                    Text(UiMessages.string(.nativeSwiftCommentThreadIllegalContent, locale: nativeUiLocale))
                        .tag("illegal_content")
                    Text(UiMessages.string(.nativeSwiftCommentThreadOther, locale: nativeUiLocale)).tag("other")
                }
                .pickerStyle(.menu)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesNote, locale: nativeUiLocale),
                    text: $composer.note,
                    axis: .vertical
                )
                .textFieldStyle(.roundedBorder)
            } else {
                NativeMarkdownEditor(client: client, markdown: $composer.markdown)
            }

            if case .edit = composer.kind {
                EmptyView()
            } else {
                Button(UiMessages.string(
                    composer.turnstileToken == nil
                        ? .nativeSwiftCommonVerify
                        : .nativeSwiftCommonVerified,
                    locale: nativeUiLocale
                )) {
                    showingTurnstile = true
                }
                .buttonStyle(.bordered)
            }

            HStack {
                Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), action: onCancel)
                Spacer()
                Button(UiMessages.string(title, locale: nativeUiLocale)) {
                    Swift.Task { await onSubmit(composer) }
                }
                .buttonStyle(.borderedProminent)
                .disabled(submitDisabled)
            }
        }
        .padding(Spacing.md)
        .sheet(isPresented: $showingTurnstile) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                composer.turnstileToken = token
                onCaptureTurnstile(token)
            }
        }
    }

    private var title: UiMessageKey {
        switch composer.kind {
        case .reply: .nativeSwiftCommentThreadActionRowReply
        case .edit: .nativeSwiftCommonSave
        case .report: .nativeSwiftCommentThreadActionRowReport
        case .delete: .nativeSwiftCommonDelete
        }
    }

    private var submitDisabled: Bool {
        switch composer.kind {
        case .reply, .edit:
            composer.markdown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        case .report:
            composer.reason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        case .delete:
            false
        }
    }

}
