import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct LandingPageContentSection: View {
    @Bindable var viewModel: LandingPagesViewModel
    let locale: Locale

    var body: some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftLandingPagesContent, locale: locale))) {
            ForEach(viewModel.draftItems, id: \.id) { item in
                itemRow(item)
            }
            Picker(
                UiMessages.string(.nativeSwiftLandingPagesItemType, locale: locale),
                selection: $viewModel.addType
            ) {
                ForEach(LandingPageAddType.allCases) { type in
                    Text(UiMessages.string(type.titleKey, locale: locale)).tag(type)
                }
            }
            pickerFields
            Button(UiMessages.string(addButtonKey, locale: locale)) {
                viewModel.addSelectedItem()
            }
            .disabled(!viewModel.canAddItem)
            Button(UiMessages.string(.nativeSwiftLandingPagesSaveContent, locale: locale)) {
                Task { await viewModel.saveContent() }
            }
            .disabled(viewModel.isLoading)
        }
    }

    @ViewBuilder
    private var pickerFields: some View {
        switch viewModel.addType {
        case .link:
            TextField(
                UiMessages.string(.nativeSwiftLandingPagesLinkLabel, locale: locale),
                text: $viewModel.linkLabel
            )
            TextField(
                UiMessages.string(.nativeSwiftLandingPagesLinkUrl, locale: locale),
                text: $viewModel.linkUrl
            )
        case .profileLink:
            candidatePicker(choices: viewModel.profileLinkChoices)
        case .review:
            candidatePicker(choices: viewModel.reviewChoices)
        case .referralLink:
            candidatePicker(choices: viewModel.referralLinkChoices)
        case .topicGroup:
            topicGroupFields
        }
    }

    private func candidatePicker(choices: [LandingPagePickerChoice]) -> some View {
        Picker(
            UiMessages.string(.nativeSwiftLandingPagesItem, locale: locale),
            selection: $viewModel.selectedCandidateID
        ) {
            Text(UiMessages.string(.nativeSwiftCommonChoose, locale: locale)).tag(String?.none)
            ForEach(choices) { choice in
                choiceText(choice).tag(Optional(choice.id))
            }
        }
    }

    @ViewBuilder
    private var topicGroupFields: some View {
        Picker(
            UiMessages.string(.nativeSwiftLandingPagesTopic, locale: locale),
            selection: $viewModel.selectedTopicID
        ) {
            Text(UiMessages.string(.nativeSwiftCommonChoose, locale: locale)).tag(String?.none)
            ForEach(viewModel.topicChoices) { choice in
                choiceText(choice).tag(Optional(choice.id))
            }
        }
        if viewModel.selectedTopicID != nil {
            Text(UiMessages.string(.nativeSwiftLandingPagesReviews, locale: locale))
                .font(Typography.caption)
            ForEach(viewModel.groupReviewChoices) { choice in
                Toggle(isOn: groupReviewBinding(choice.id)) { choiceText(choice) }
            }
            Text(UiMessages.string(.nativeSwiftLandingPagesReferralLinks, locale: locale))
                .font(Typography.caption)
            ForEach(viewModel.groupReferralLinkChoices) { choice in
                Toggle(isOn: groupReferralLinkBinding(choice.id)) { choiceText(choice) }
            }
        }
    }

    private func itemRow(_ item: LandingPageItem) -> some View {
        HStack {
            Text(verbatim: UiMessages.string(item.titleText, locale: locale))
            Spacer()
            Button(UiMessages.string(.nativeSwiftCommonUp, locale: locale)) {
                viewModel.moveItem(id: item.id, direction: -1)
            }
            Button(UiMessages.string(.nativeSwiftCommonDown, locale: locale)) {
                viewModel.moveItem(id: item.id, direction: 1)
            }
            Button(UiMessages.string(.nativeSwiftCommonRemove, locale: locale)) {
                viewModel.removeItem(id: item.id)
            }
        }
        .buttonStyle(.borderless)
        .disabled(viewModel.isLoading)
    }

    private func choiceText(_ choice: LandingPagePickerChoice) -> Text {
        Text(verbatim: UiMessages.string(choice.label, locale: locale))
    }

    private func groupReviewBinding(_ id: String) -> Binding<Bool> {
        Binding(
            get: { viewModel.selectedGroupReviewIDs.contains(id) },
            set: { selected in
                if selected {
                    viewModel.selectedGroupReviewIDs.insert(id)
                } else {
                    viewModel.selectedGroupReviewIDs.remove(id)
                }
            }
        )
    }

    private func groupReferralLinkBinding(_ id: String) -> Binding<Bool> {
        Binding(
            get: { viewModel.selectedGroupReferralLinkIDs.contains(id) },
            set: { selected in
                if selected {
                    viewModel.selectedGroupReferralLinkIDs.insert(id)
                } else {
                    viewModel.selectedGroupReferralLinkIDs.remove(id)
                }
            }
        )
    }

    private var addButtonKey: UiMessageKey {
        viewModel.addType == .link ? .nativeSwiftLandingPagesAddLink : .nativeSwiftLandingPagesAddItem
    }

}

private extension LandingPageItem {
    var titleText: UiVerbatimText {
        switch self {
        case let .profileLink(_, profileLink):
            if let title = profileLink.name ?? profileLink.handle ?? profileLink.url {
                return .userContent(title)
            }
            return .message(.nativeSwiftLandingPagesProfileLink)
        case let .review(_, review):
            return .userContent(review.title)
        case let .referralLink(_, referralLink):
            return .userContent(referralLink.label ?? referralLink.referralProgramName)
        case let .topicGroup(_, topic, entries):
            return .message(
                .nativeSwiftLandingPagesTopicEntryCount,
                textParameters: ["topic": .userContent(topic.name)],
                numberParameters: ["count": Double(entries.count)]
            )
        case let .link(_, label, _):
            return .userContent(label)
        }
    }
}
