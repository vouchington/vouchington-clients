import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

extension NativeTopicRecommendationSurface {
    func topicFields(viewModel: NativeTopicRecommendationViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            TextField(
                UiMessages.string(.nativeSwiftTopicManagementFieldsPrimaryHostname, locale: nativeUiLocale),
                text: Bindable(viewModel).topicHostname
            )
            .textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftTopicRecommendationAliasesOnePerLine, locale: nativeUiLocale),
                text: Bindable(viewModel).aliases,
                axis: .vertical
            )
            .textFieldStyle(.roundedBorder)
            TextEditor(text: Bindable(viewModel).topicMarkdown)
                .frame(minHeight: 96)
                .overlay(editorBorder)
            if viewModel.topicType == "referral_program" {
                TextField(
                    UiMessages.string(.nativeSwiftTopicRecommendationExampleReferralLink, locale: nativeUiLocale),
                    text: Bindable(viewModel).exampleReferralLink
                )
                .textFieldStyle(.roundedBorder)
            }
            if viewModel.topicType == "card" {
                TextField(
                    UiMessages.string(.nativeSwiftTopicRecommendationLandingPageUrlsOnePerLine, locale: nativeUiLocale),
                    text: Bindable(viewModel).landingPageUrls,
                    axis: .vertical
                )
                .textFieldStyle(.roundedBorder)
            }
            TextField(
                UiMessages.string(.nativeSwiftTopicRecommendationRelatedHostnamesOnePerLine, locale: nativeUiLocale),
                text: Bindable(viewModel).topicHostnames,
                axis: .vertical
            )
            .textFieldStyle(.roundedBorder)
        }
    }

    var editorBorder: some View {
        RoundedRectangle(cornerRadius: 8, style: .continuous)
            .strokeBorder(.quaternary, lineWidth: 1)
    }

    func contributionAdmissionMessage(_ error: VouchaError) -> String? {
        let key: UiMessageKey? = if case let .api(_, code) = error {
            NativeContributionAdmissionPresentation.messageKey(code)
        } else {
            nil
        }
        return key.map { UiMessages.string($0, locale: nativeUiLocale) }
    }
}
