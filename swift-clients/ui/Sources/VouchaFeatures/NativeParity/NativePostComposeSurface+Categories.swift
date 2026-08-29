import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativePostComposeSurface {
    func discussionCategorySection(viewModel: NativePostComposeViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftTagManagementCategories, locale: nativeUiLocale))
                .font(Typography.headline)

            ForEach(viewModel.categoryDrafts) { category in
                HStack(spacing: Spacing.sm) {
                    Picker(
                        UiMessages.string(.nativeSwiftTagManagementCategories, locale: nativeUiLocale),
                        selection: Binding(
                            get: {
                                viewModel.categoryDrafts.first(where: { $0.id == category.id })?.type ?? category.type
                            },
                            set: { viewModel.updateCategoryType(id: category.id, type: $0) }
                        )
                    ) {
                        Text(UiMessages.string(.nativeSwiftTagManagementCategoryTopics, locale: nativeUiLocale))
                            .tag(NativePostComposeCategoryType.topic)
                        Text(UiMessages.string(.nativeSwiftTopHashtagsSearchHashtags, locale: nativeUiLocale))
                            .tag(NativePostComposeCategoryType.hashtag)
                    }
                    .pickerStyle(.segmented)

                    TextField(
                        UiMessages.string(
                            category.type == .topic
                                ? .nativeSwiftPostTypeSpecificFieldsTopicId
                                : .nativeSwiftTopHashtagsSearchHashtags,
                            locale: nativeUiLocale
                        ),
                        text: Binding(
                            get: { viewModel.categoryDrafts.first(where: { $0.id == category.id })?.value ?? "" },
                            set: { viewModel.updateCategoryValue(id: category.id, value: $0) }
                        )
                    )
                    .textFieldStyle(.roundedBorder)

                    Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale)) {
                        viewModel.removeCategory(id: category.id)
                    }
                    .buttonStyle(.borderless)
                }
            }

            Button(UiMessages.string(.nativeSwiftCommonAdd, locale: nativeUiLocale)) {
                viewModel.addCategory()
            }
            .buttonStyle(.bordered)
        }
    }
}
