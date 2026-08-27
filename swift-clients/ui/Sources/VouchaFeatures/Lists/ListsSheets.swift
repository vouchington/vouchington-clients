import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ListsCreateSheet: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.dismiss)
    private var dismiss
    @State
    private var name = ""
    let onSave: (String) async -> Void

    var body: some View {
        ListsFormShell(
            title: .nativeSwiftListsCreateList,
            saveTitle: .nativeSwiftCommonCreate,
            canSave: !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        ) {
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $name)
                .textFieldStyle(.roundedBorder)
        } save: {
            await onSave(name)
            dismiss()
        }
    }
}

struct ListsEditSheet: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.dismiss)
    private var dismiss
    @State
    private var name: String
    @State
    private var description: String
    let onSave: (String, String?) async -> Void

    init(list: UserList, onSave: @escaping (String, String?) async -> Void) {
        _name = State(initialValue: list.name)
        _description = State(initialValue: list.description ?? "")
        self.onSave = onSave
    }

    var body: some View {
        ListsFormShell(
            title: .nativeSwiftListsEditList,
            saveTitle: .nativeSwiftCommonSave,
            canSave: !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        ) {
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $name)
                .textFieldStyle(.roundedBorder)
            TextEditor(text: $description)
                .font(Typography.body)
                .frame(minHeight: 96)
                .overlay(RoundedRectangle(cornerRadius: 8).stroke(Colors.separator, lineWidth: 1))
        } save: {
            await onSave(name, description)
            dismiss()
        }
    }
}

struct ListsImportSheet: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.dismiss)
    private var dismiss
    @State
    private var slug = ""
    let onImport: (String) async -> Void

    var body: some View {
        ListsFormShell(
            title: .nativeSwiftListsImportCommunity,
            saveTitle: .nativeSwiftCommonImport,
            canSave: !slug.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        ) {
            TextField(UiMessages.string(.nativeSwiftListsCommunitySlug, locale: nativeUiLocale), text: $slug)
                .textFieldStyle(.roundedBorder)
        } save: {
            await onImport(slug)
            dismiss()
        }
    }
}

struct ListsFormShell<Content: View>: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.dismiss)
    private var dismiss
    let title: UiMessageKey
    let saveTitle: UiMessageKey
    let canSave: Bool
    @ViewBuilder
    let content: Content
    let save: () async -> Void
    @State
    private var isSaving = false

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Spacing.md) {
                content
                    .disabled(isSaving)
                Spacer()
            }
            .padding(Spacing.lg)
            .navigationTitle(UiMessages.string(title, locale: nativeUiLocale))
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale)) { dismiss() }
                        .disabled(isSaving)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(UiMessages.string(saveTitle, locale: nativeUiLocale)) {
                        guard canSave, !isSaving else { return }
                        isSaving = true
                        Task { @MainActor in
                            defer { isSaving = false }
                            await save()
                        }
                    }
                    .disabled(!canSave || isSaving)
                }
            }
        }
    }
}
