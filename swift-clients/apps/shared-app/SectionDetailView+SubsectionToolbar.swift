import SwiftUI
import VouchaFeatures
import VouchaLocalization

extension SectionDetailView {
    #if !os(macOS)
        /// A fixed-size segmented picker is wider than the space between Sign In and Customize
        /// on a compact iPhone, so it covers the leading sign-in button. Signed-in bars have no
        /// sign-in button and keep that picker. Signed-out "your" subsections already normalize
        /// to the public feed, so the menu lists only destinations that stay selected.
        @ToolbarContentBuilder
        var verticalPickerToolbarItem: some ToolbarContent {
            if section.isVertical, factory.sessionManager.isSignedIn, !section.subsections.isEmpty {
                ToolbarItem(placement: .principal) {
                    signedInSubsectionPicker
                }
            } else if section.isVertical, signedOutToolbarSubsections.count > 1 {
                ToolbarItem(placement: .principal) {
                    signedOutSubsectionMenu
                }
            }
        }

        private var signedOutToolbarSubsections: [VerticalSubsection] {
            section.subsections.filter { normalizeSignedOutSubsection($0) == $0 }
        }

        private var signedInSubsectionPicker: some View {
            Picker(selection: Binding(get: { effectiveSubsection }, set: selectIOSSubsection)) {
                ForEach(section.subsections) { subsection in
                    Text(subsectionToolbarTitle(subsection)).tag(subsection)
                }
            } label: {
                EmptyView()
            }
            .pickerStyle(.segmented)
            .fixedSize()
        }

        private var signedOutSubsectionMenu: some View {
            Menu {
                ForEach(signedOutToolbarSubsections) { subsection in
                    Button(subsectionToolbarTitle(subsection)) {
                        selectIOSSubsection(subsection)
                    }
                }
            } label: {
                Text(subsectionToolbarTitle(effectiveSubsection))
            }
        }

        private func subsectionToolbarTitle(_ subsection: VerticalSubsection) -> String {
            UiMessages.string(section.subsectionTitle(subsection), locale: nativeUiLocale)
        }
    #endif
}
