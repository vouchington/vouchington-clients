import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeReferralLinksRows: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeReferralLinksManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftReferralLinksManagementMyReferralLinks, locale: nativeUiLocale))
                .font(.headline)
            if viewModel.links.isEmpty {
                EmptyStateView(
                    icon: "link",
                    title: .message(.nativeSwiftEmptyStateNoReferralLinks),
                    message: .message(.nativeSwiftEmptyStateNoReferralLinksMessage)
                )
            } else {
                ForEach(groupedLinks) { group in
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(group.programName)
                            .font(.subheadline)
                            .fontWeight(.semibold)
                        ForEach(group.links) { link in
                            NativeReferralLinkRow(viewModel: viewModel, link: link)
                        }
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.hasMoreLinks,
                    isLoading: viewModel.linkPagination.isLoading,
                    hasError: viewModel.linkPagination.lastError != nil
                ) {
                    await viewModel.loadMoreLinks()
                }
            }
        }
    }

    private var groupedLinks: [NativeReferralLinkProgramGroup] {
        Dictionary(grouping: viewModel.links, by: {
            $0.referralProgramName
                ?? UiMessages.string(.nativeSwiftReferralLinksManagementReferralProgram, locale: nativeUiLocale)
        })
        .map { programName, links in
            NativeReferralLinkProgramGroup(
                programName: programName,
                links: links.sorted { lhs, rhs in
                    (lhs.label ?? lhs.url ?? lhs.id).localizedCaseInsensitiveCompare(rhs.label ?? rhs.url ?? rhs.id)
                        == .orderedAscending
                }
            )
        }
        .sorted { lhs, rhs in
            lhs.programName.localizedCaseInsensitiveCompare(rhs.programName) == .orderedAscending
        }
    }
}

private struct NativeReferralLinkProgramGroup: Identifiable {
    let programName: String
    let links: [NativeReferralLink]

    var id: String {
        programName
    }
}

private struct NativeReferralLinkRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeReferralLinksManagementViewModel
    let link: NativeReferralLink
    @State
    private var isRenaming = false
    @State
    private var renameLabel = ""
    @State
    private var isConfirmingDelete = false

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(
                link.label
                    ?? link.referralProgramName
                    ?? UiMessages.string(.nativeSwiftReferralLinksManagementReferralLink, locale: nativeUiLocale)
            )
            .font(.headline)
            if let url = link.url {
                Text(url)
                    .font(.caption)
            }
            HStack {
                Button(UiMessages.string(.nativeSwiftChatRename, locale: nativeUiLocale)) {
                    renameLabel = link.label ?? link.referralProgramName ?? ""
                    isRenaming = true
                }
                Button(UiMessages.string(
                    link.deactivatedAt == nil
                        ? .nativeSwiftReferralLinksManagementDeactivate
                        : .nativeSwiftReferralLinksManagementActivate,
                    locale: nativeUiLocale
                )) {
                    Task { await viewModel.setActive(id: link.id, active: link.deactivatedAt != nil) }
                }
                Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                    isConfirmingDelete = true
                }
            }
            .buttonStyle(.bordered)
        }
        .padding(.vertical, Spacing.xs)
        .alert(
            UiMessages.string(.nativeSwiftReferralLinksManagementRenameReferralLink, locale: nativeUiLocale),
            isPresented: $isRenaming
        ) {
            TextField(
                UiMessages.string(.nativeSwiftReferralLinksManagementLabel, locale: nativeUiLocale),
                text: $renameLabel
            )
            Button(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale)) {
                let nextLabel = renameLabel.trimmingCharacters(in: .whitespacesAndNewlines)
                Task { await viewModel.rename(id: link.id, label: nextLabel.isEmpty ? nil : nextLabel) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {}
        }
        .confirmationDialog(
            UiMessages
                .string(.nativeSwiftReferralLinksManagementDeleteReferralLinkConfirmationTitle, locale: nativeUiLocale),
            isPresented: $isConfirmingDelete,
            titleVisibility: .visible
        ) {
            Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                Task { await viewModel.delete(id: link.id) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {}
        }
    }
}
