import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativePostTypeSpecificFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativePostComposeViewModel

    var body: some View {
        switch viewModel.postType {
        case .review:
            reviewFields
        case .dataPoint:
            dataPointFields
        default:
            EmptyView()
        }
    }

    private var reviewFields: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.reviewTopicRatings) { rating in
                HStack(spacing: Spacing.sm) {
                    TextField(
                        UiMessages.string(.nativeSwiftPostTypeSpecificFieldsTopicId, locale: nativeUiLocale),
                        text: Binding(
                            get: { rating.topicId },
                            set: { updateReviewTopicRating(id: rating.id, topicId: $0) }
                        )
                    )
                    .textFieldStyle(.roundedBorder)

                    Stepper(
                        UiMessages.string(
                            .nativeSwiftPresentationValuesRatingValue,
                            parameters: ["rating": UiMessages.number(rating.rating, locale: nativeUiLocale)],
                            locale: nativeUiLocale
                        ),
                        value: Binding(
                            get: { rating.rating },
                            set: { updateReviewTopicRating(id: rating.id, rating: $0) }
                        ),
                        in: 0 ... 5
                    )

                    Button {
                        removeReviewTopicRating(id: rating.id)
                    } label: {
                        Image(systemName: "minus.circle")
                    }
                    .buttonStyle(.borderless)
                }
            }

            Button {
                viewModel.addReviewTopicRating()
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftPostTypeSpecificFieldsAddTopic, locale: nativeUiLocale),
                    systemImage: "plus.circle"
                )
            }
            .buttonStyle(.bordered)
        }
    }

    private func updateReviewTopicRating(id: UUID, topicId: String? = nil, rating: Int? = nil) {
        guard let index = viewModel.reviewTopicRatings.firstIndex(where: { $0.id == id }) else { return }
        if let topicId {
            viewModel.reviewTopicRatings[index].topicId = topicId
        }
        if let rating {
            viewModel.reviewTopicRatings[index].rating = rating
        }
    }

    private func removeReviewTopicRating(id: UUID) {
        guard let index = viewModel.reviewTopicRatings.firstIndex(where: { $0.id == id }) else { return }
        viewModel.removeReviewTopicRating(at: index)
    }

    private var dataPointFields: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Picker(UiMessages.string(.nativeSwiftPresentationVertical, locale: nativeUiLocale), selection: Binding(
                get: { viewModel.dataPointVertical },
                set: { viewModel.dataPointVertical = $0 }
            )) {
                Text(UiMessages.string(.nativeSwiftCommonChoose, locale: nativeUiLocale)).tag(DataPointVertical?.none)
                Text(UiMessages.string(.nativeSwiftPostTypeSpecificFieldsCreditCard, locale: nativeUiLocale))
                    .tag(Optional.some(DataPointVertical.creditCard))
                Text(UiMessages.string(.nativeSwiftPostTypeSpecificFieldsBankAccount, locale: nativeUiLocale))
                    .tag(Optional.some(DataPointVertical.bankAccount))
            }
            .pickerStyle(.segmented)

            TextEditor(text: Binding(
                get: { viewModel.dataPointStructuredDataJSON },
                set: { viewModel.dataPointStructuredDataJSON = $0 }
            ))
            .frame(minHeight: 96)
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(.quaternary, lineWidth: 1)
            )
        }
    }
}
