import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeTopicRecommendationSurface {
    @ViewBuilder
    func status(viewModel: NativeTopicRecommendationViewModel) -> some View {
        switch viewModel.state {
        case .required:
            Text(UiMessages.string(.nativeSwiftPostComposeVerificationTokenRequired, locale: nativeUiLocale))
                .foregroundStyle(Colors.secondaryLabel)
        case let .error(error):
            Text(contributionAdmissionMessage(error) ?? error.localizedDescription)
                .foregroundStyle(Colors.negativeVote)
                .accessibilityAddTraits(.isStaticText)
        default:
            if let saved = viewModel.savedPostId {
                Text(UiMessages.string(
                    .nativeSwiftTopicRecommendationSavedValue,
                    parameters: ["value": UiMessages.string(.verbatim(saved), locale: nativeUiLocale)],
                    locale: nativeUiLocale
                ))
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

}
