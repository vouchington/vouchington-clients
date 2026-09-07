import Foundation
import VouchaLocalization

struct NormalizedAuthoredText {
    let value: String
    let text: UiVerbatimText
    let declaredLanguage: String?
    let detectedLanguage: String?

    init?(text: String?, declaredLanguage: String?, detectedLanguage: String?) {
        guard let text = text?.trimmingCharacters(in: .whitespacesAndNewlines), !text.isEmpty else {
            return nil
        }
        value = text
        self.text = .userContent(text)
        self.declaredLanguage = declaredLanguage
        self.detectedLanguage = detectedLanguage
    }
}
