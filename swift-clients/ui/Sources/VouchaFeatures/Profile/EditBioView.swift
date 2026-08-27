import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

/// Sheet for editing the user's bio markdown.
struct EditBioView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.dismiss)
    private var dismiss
    @State
    private var bio: String
    @State
    private var isLoading = false
    @State
    private var errorMessage: UiVerbatimText?

    private let client: APIClient?
    private let onSave: (String) async throws -> Void

    init(client: APIClient?, currentBio: String, onSave: @escaping (String) async throws -> Void) {
        self.client = client
        _bio = State(initialValue: currentBio)
        self.onSave = onSave
    }

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(UiMessages.string(.nativeSwiftProfileEditYourBioMarkdownIsSupported, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                NativeMarkdownEditor(client: client, markdown: $bio, minHeight: 140)
                if let error = errorMessage {
                    Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.negativeVote)
                }
                Spacer()
            }
            .padding(Spacing.lg)
            .navigationTitle(UiMessages.string(.nativeSwiftProfileEditBio, locale: nativeUiLocale))
            #if os(iOS)
                .navigationBarTitleDisplayMode(.inline)
            #endif
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale)) { dismiss() }
                    }
                    ToolbarItem(placement: .confirmationAction) {
                        saveButton
                    }
                }
        }
    }

    private var saveButton: some View {
        Button {
            Task { await save() }
        } label: {
            if isLoading {
                ProgressView()
            } else {
                Text(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale))
            }
        }
        .disabled(isLoading)
    }

    @MainActor
    private func save() async {
        isLoading = true
        errorMessage = nil
        do {
            try await onSave(bio)
            dismiss()
        } catch {
            errorMessage = .verbatim(error.localizedDescription)
        }
        isLoading = false
    }
}
