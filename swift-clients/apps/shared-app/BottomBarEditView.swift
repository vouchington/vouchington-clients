import SwiftUI
import VouchaFeatures
import VouchaLocalization

struct BottomBarEditView: View {
    @Binding
    var preferences: AppSectionPreferences
    let onSave: (AppSectionPreferences) -> Void

    @Environment(\.dismiss)
    private var dismiss
    @Environment(\.locale)
    private var locale

    var body: some View {
        NavigationStack {
            List {
                Section {
                    ForEach(preferences.orderedSections) { section in
                        Toggle(isOn: binding(for: section)) {
                            Label(UiMessages.string(section.titleKey, locale: locale), systemImage: section.systemImage)
                        }
                    }
                    .onMove { source, destination in
                        preferences.move(fromOffsets: source, toOffset: destination)
                        normalizePreferences()
                    }
                } header: {
                    Text(UiMessages.string(.nativeNavigationSections, locale: locale))
                } footer: {
                    Text(UiMessages.string(.nativeNavigationCustomizeDescription, locale: locale))
                }
            }
            .navigationTitle(UiMessages.string(.nativeNavigationCustomize, locale: locale))
            #if os(iOS)
                .navigationBarTitleDisplayMode(.inline)
            #endif
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button(UiMessages.string(.nativeCommonDone, locale: locale)) {
                            dismiss()
                        }
                    }
                    #if os(iOS)
                        ToolbarItem(placement: .primaryAction) {
                            EditButton()
                        }
                    #endif
                }
        }
        .onDisappear {
            persist()
        }
    }

    private func binding(for section: AppSection) -> Binding<Bool> {
        Binding(
            get: { !preferences.hiddenSectionIDs.contains(section.rawValue) },
            set: { newValue in
                preferences.setHidden(section, hidden: !newValue)
                normalizePreferences()
            }
        )
    }

    private func normalizePreferences() {
        var normalized = preferences
        normalized.normalize()
        preferences = normalized
    }

    private func persist() {
        normalizePreferences()
        let normalized = preferences
        onSave(normalized)
    }
}
