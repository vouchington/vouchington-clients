import VouchaLocalization

struct LandingPageReviewDisplay {
    let text: UiVerbatimText
    let declaredLanguage: String?
    let detectedLanguage: String?

    init(
        text: UiVerbatimText,
        declaredLanguage: String? = nil,
        detectedLanguage: String? = nil
    ) {
        self.text = text
        self.declaredLanguage = declaredLanguage
        self.detectedLanguage = detectedLanguage
    }
}

extension LandingPageReview {
    var landingPageReviewDisplay: LandingPageReviewDisplay {
        if let title = landingPageFirstNonempty(title) {
            return .init(
                text: .userContent(title),
                declaredLanguage: declaredLanguage,
                detectedLanguage: linguaRsDetectedLanguage
            )
        }
        if let preview = landingPageFirstNonempty(String(markdown.prefix(40))) {
            return .init(
                text: .userContent(preview),
                declaredLanguage: declaredLanguage,
                detectedLanguage: linguaRsDetectedLanguage
            )
        }
        return .init(text: .message(.nativeSwiftLandingPagesReview))
    }
}

private func landingPageFirstNonempty(_ values: String?...) -> String? {
    values.lazy.compactMap { value in
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmed.isEmpty ? nil : trimmed
    }.first
}
