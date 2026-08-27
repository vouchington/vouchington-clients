import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

@MainActor
struct NativeMarkdownEditor: View {
    @Environment(\.locale)
    var nativeUiLocale
    enum Mode: CaseIterable {
        case write
        case preview

        var title: UiMessageKey {
            switch self {
            case .write: .nativeSwiftMarkdownEditorWrite
            case .preview: .nativeSwiftMarkdownEditorPreview
            }
        }
    }

    let client: APIClient?
    @Binding
    var markdown: String
    var minHeight: CGFloat = 180

    @State
    private var mode: Mode = .write
    @State
    private var previewHtml = ""
    @State
    private var previewState: LoadState = .idle
    @State
    private var previewRequest = 0
    @State
    var suggestions: [MarkdownAutocompleteSuggestion] = []
    @State
    var autocompleteToken: MarkdownAutocompleteToken?
    @State
    var autocompleteTask: Task<Void, Never>?

    init(
        client: APIClient?,
        markdown: Binding<String>,
        minHeight: CGFloat = 180,
        initialMode: Mode = .write,
        initialPreviewHtml: String = "",
        initialPreviewState: LoadState = .idle,
        initialSuggestions: [MarkdownAutocompleteSuggestion] = []
    ) {
        self.client = client
        _markdown = markdown
        self.minHeight = minHeight
        _mode = State(initialValue: initialMode)
        _previewHtml = State(initialValue: initialPreviewHtml)
        _previewState = State(initialValue: initialPreviewState)
        _suggestions = State(initialValue: initialSuggestions)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Picker(
                UiMessages.string(.nativeSwiftMarkdownEditorMarkdownMode, locale: nativeUiLocale),
                selection: $mode
            ) {
                ForEach(Mode.allCases, id: \.self) { mode in
                    Text(UiMessages.string(mode.title, locale: nativeUiLocale)).tag(mode)
                }
            }
            .pickerStyle(.segmented)
            .onChange(of: mode) { _, nextMode in
                if nextMode == .preview {
                    Task { await refreshPreview() }
                }
            }

            if mode == .write {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    TextEditor(text: $markdown)
                        .frame(minHeight: minHeight)
                        .overlay(editorBorder)
                        .onChange(of: markdown) { _, value in
                            scheduleAutocomplete(for: value)
                        }

                    if !suggestions.isEmpty {
                        VStack(alignment: .leading, spacing: 0) {
                            ForEach(suggestions) { suggestion in
                                Button {
                                    insert(suggestion)
                                } label: {
                                    HStack {
                                        Text(suggestion.label)
                                            .font(Typography.body)
                                            .foregroundStyle(.primary)
                                        if let detail = suggestion.detail {
                                            Text(detail)
                                                .font(Typography.caption)
                                                .foregroundStyle(Colors.secondaryLabel)
                                        }
                                        Spacer()
                                    }
                                    .padding(.horizontal, Spacing.sm)
                                    .padding(.vertical, Spacing.xs)
                                }
                                .buttonStyle(.plain)
                            }
                        }
                        .background(.background)
                        .overlay(editorBorder)
                    }
                }
            } else {
                preview
                    .frame(minHeight: minHeight, alignment: .topLeading)
                    .padding(Spacing.sm)
                    .overlay(editorBorder)
            }
        }
    }

    @ViewBuilder
    private var preview: some View {
        switch previewState {
        case .idle:
            if markdown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Text(UiMessages.string(.nativeSwiftMarkdownEditorNothingToPreviewYet, locale: nativeUiLocale))
                    .font(Typography.body)
                    .foregroundStyle(Colors.secondaryLabel)
            } else {
                NativeHtmlContent(html: previewHtml, fallback: markdown)
            }
        case .loading:
            ProgressView()
        case .loaded:
            NativeHtmlContent(html: previewHtml, fallback: markdown)
        case .error:
            Text(UiMessages.string(.nativeSwiftMarkdownEditorPreviewUnavailable, locale: nativeUiLocale))
                .font(Typography.body)
                .foregroundStyle(Colors.negativeVote)
        }
    }

}

extension NativeMarkdownEditor {
    private var editorBorder: some View {
        RoundedRectangle(cornerRadius: 8, style: .continuous)
            .strokeBorder(.quaternary, lineWidth: 1)
    }

    func refreshPreview() async {
        let nextRequest = previewRequest + 1
        previewRequest = nextRequest
        let trimmed = markdown.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            previewHtml = ""
            previewState = .idle
            return
        }
        guard let client else {
            previewState = .error(.api(statusCode: 0, preconditionCode: nil))
            return
        }

        previewState = .loading
        do {
            let response: MarkdownPreviewResponse = try await client.send(.markdownPreview(markdown: markdown))
            guard previewRequest == nextRequest else { return }
            previewHtml = response.html
            previewState = .loaded
        } catch let error as VouchaError {
            guard previewRequest == nextRequest else { return }
            previewState = .error(error)
        } catch {
            guard previewRequest == nextRequest else { return }
            previewState = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }
}
