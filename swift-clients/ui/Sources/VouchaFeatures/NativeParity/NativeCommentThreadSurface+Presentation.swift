import SwiftUI
import VouchaLocalization

extension NativeCommentThreadSurface {
    func composerSheet(for composer: NativeCommentThreadComposerState) -> some View {
        NativeCommentThreadComposerSheet(
            composer: composer,
            showingTurnstile: showingTurnstileBinding,
            viewModel: viewModel,
            client: client,
            turnstileSiteKey: turnstileSiteKey,
            onSubmit: submitComposer,
            onCancel: { self.composer = nil },
            onCaptureTurnstile: { _ in }
        )
    }

    var sortPicker: some View {
        Picker(UiMessages.string(.nativeSwiftCommentThreadSort, locale: nativeUiLocale), selection: $viewModel.sort) {
            Text(UiMessages.string(.nativeSwiftChatNew, locale: nativeUiLocale)).tag(NativeCommentThreadSort.new)
            Text(UiMessages.string(.nativeSwiftCommentThreadBest, locale: nativeUiLocale))
                .tag(NativeCommentThreadSort.best)
        }
        .pickerStyle(.segmented)
    }
}
