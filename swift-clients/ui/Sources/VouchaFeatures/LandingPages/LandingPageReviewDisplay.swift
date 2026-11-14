import VouchaAPI
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

extension LandingPageReviewDisplay {
    init(title: String, markdown: String, declaredLanguage: String?, detectedLanguage: String?) {
        if let title = landingPageFirstNonempty(title) {
            self.init(
                text: .userContent(title),
                declaredLanguage: declaredLanguage,
                detectedLanguage: detectedLanguage
            )
            return
        }
        if let preview = landingPageFirstNonempty(String(markdown.prefix(40))) {
            self.init(
                text: .userContent(preview),
                declaredLanguage: declaredLanguage,
                detectedLanguage: detectedLanguage
            )
            return
        }
        self.init(text: .message(.nativeSwiftLandingPagesReview))
    }
}

extension LandingPageReview {
    var landingPageReviewDisplay: LandingPageReviewDisplay {
        .init(
            title: title, markdown: markdown,
            declaredLanguage: declaredLanguage, detectedLanguage: linguaRsDetectedLanguage
        )
    }
}

extension VouchaAPI.LandingPageReview {
    var landingPageReviewDisplay: LandingPageReviewDisplay {
        .init(
            title: title, markdown: markdown,
            declaredLanguage: declaredLanguage, detectedLanguage: linguaRsDetectedLanguage
        )
    }
}

private func landingPageFirstNonempty(_ values: String?...) -> String? {
    values.lazy.compactMap { value in
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmed.isEmpty ? nil : trimmed
    }.first
}
