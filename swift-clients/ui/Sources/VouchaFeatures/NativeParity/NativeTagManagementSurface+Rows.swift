import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeTagPublisherPicker: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: NativeTagManagementViewModel

    var body: some View {
        Picker(
            UiMessages.string(viewModel.tagPickerTitle, locale: nativeUiLocale),
            selection: Binding(
                get: { viewModel.publisherTypeSelection },
                set: { viewModel.publisherTypeSelection = $0 }
            )
        ) {
            Text(UiMessages.string(viewModel.tagPickerPlaceholder, locale: nativeUiLocale)).tag("")
            ForEach(viewModel.availablePublisherTypes) { option in
                Text(option.label).tag(option.id)
            }
        }
        .pickerStyle(.menu)
        .disabled(viewModel.availablePublisherTypes.isEmpty || viewModel.isMutating)
    }
}

struct NativeTagSearchForm: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: NativeTagManagementViewModel
    let config: NativeTagRelationTab

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            TextField(UiMessages.string(
                .nativeSwiftTagManagementSearchObjects,
                parameters: ["objectType": UiMessages.string(.verbatim(config.objectType), locale: nativeUiLocale)],
                locale: nativeUiLocale
            ), text: $viewModel.searchQuery)
                .textFieldStyle(.roundedBorder)

            if case .loading = viewModel.searchState {
                ProgressView()
            } else if viewModel.searchResults.isEmpty {
                Text(UiMessages.string(
                    viewModel.searchQuery.trimmed.isEmpty
                        ? .nativeSwiftTagManagementSearchToAddTags
                        : .nativeSwiftTagManagementNoResults,
                    locale: nativeUiLocale
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            } else {
                NativeTagSearchResults(viewModel: viewModel)
            }
        }
    }
}

struct NativeTagSearchResults: View {
    let viewModel: NativeTagManagementViewModel

    var body: some View {
        LazyVStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(viewModel.searchResults, id: \.id) { result in
                Button {
                    Task { await viewModel.addSelectedResult(id: result.id) }
                } label: {
                    NativeTagSearchResultRow(result: result)
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isMutating)
            }
        }
    }
}

struct NativeTagSearchResultRow: View {
    let result: NativeGenericEntity

    var body: some View {
        HStack(spacing: Spacing.sm) {
            Image(systemName: "plus.circle")
            VStack(alignment: .leading, spacing: 2) {
                Text(result.displayTitle(fallback: result.id))
                    .font(Typography.subheadline)
                Text(result.displayDetail)
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer(minLength: 0)
        }
    }
}

struct NativeTagRelationList: View {
    let viewModel: NativeTagManagementViewModel
    let canCreateVote: Bool

    var body: some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.relations) { relation in
                NativeTagRelationRow(
                    relation: relation,
                    existingVote: viewModel.electionVotes[relation.id]?.choice,
                    canCreateVote: canCreateVote,
                    onVote: { choice in
                        Task { await viewModel.vote(relationId: relation.id, choice: choice) }
                    },
                    isDisabled: viewModel.inFlightMutationKeys.contains("vote:\(relation.id)")
                )
            }
        }
    }
}

private struct NativeTagRelationRow: View {
    let relation: EntityRelation
    let existingVote: ElectionVoteChoice?
    let canCreateVote: Bool
    let onVote: (ElectionVoteChoice?) -> Void
    let isDisabled: Bool

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(relation.objectData.displayTitle ?? relation.objectId ?? relation.id)
                    .font(Typography.body)
                    .fixedSize(horizontal: false, vertical: true)
                if let detail = relation.objectData.displayDetail {
                    Text(detail)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            Spacer(minLength: 0)
            Menu {
                if canCreateVote {
                    Button(UiMessages.string(.extractedVotesSemanticVoteConfirm, locale: Locale.current)) {
                        onVote(.confirm)
                    }
                    Button(UiMessages.string(.extractedVotesSemanticVoteDispute, locale: Locale.current)) {
                        onVote(.dispute)
                    }
                }
                if existingVote != nil {
                    if canCreateVote {
                        Divider()
                    }
                    Button(
                        UiMessages.string(.extractedVotesSemanticVoteClear, locale: Locale.current),
                        role: .destructive
                    ) { onVote(nil) }
                }
            } label: { Image(systemName: "hand.thumbsup").frame(minWidth: 44, minHeight: 44) }
                .disabled(isDisabled || (!canCreateVote && existingVote == nil))
        }
        .padding(.vertical, Spacing.xs)
    }
}
