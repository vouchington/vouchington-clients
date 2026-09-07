import Foundation
import VouchaLocalization

public struct NativeRouteDestinationRow: Identifiable, Hashable, Sendable {
    public let id: String
    public let icon: String
    public let title: String
    public let detail: String
    let titleText: UiVerbatimText
    private let detailText: UiVerbatimText
    public let externalURL: URL?
    public let targetPath: String?
    public let declaredLanguage: String?
    public let detectedLanguage: String?
    public let detailDeclaredLanguage: String?
    public let detailDetectedLanguage: String?

    public init(
        id: String = UUID().uuidString,
        icon: String,
        title: UiVerbatimText,
        detail: UiVerbatimText,
        declaredLanguage: String? = nil,
        detectedLanguage: String? = nil,
        detailDeclaredLanguage: String? = nil,
        detailDetectedLanguage: String? = nil,
        externalURL: URL? = nil,
        targetPath: String? = nil
    ) {
        self.id = id
        self.icon = icon
        titleText = title
        detailText = detail
        self.title = UiMessages.string(title, locale: .english)
        self.detail = UiMessages.string(detail, locale: .english)
        self.declaredLanguage = declaredLanguage
        self.detectedLanguage = detectedLanguage
        self.detailDeclaredLanguage = detailDeclaredLanguage
        self.detailDetectedLanguage = detailDetectedLanguage
        self.externalURL = externalURL
        self.targetPath = targetPath
    }

    public init(icon: String, title: UiMessage, detail: UiMessage, externalURL: URL? = nil, targetPath: String? = nil) {
        self.init(
            id: UUID().uuidString,
            icon: icon,
            title: .app(title),
            detail: .app(detail),
            externalURL: externalURL,
            targetPath: targetPath
        )
    }

    public func localizedTitle(locale: Locale, timeZone: TimeZone = .current) -> String {
        UiMessages.string(titleText, locale: locale, timeZone: timeZone)
    }

    public func localizedDetail(locale: Locale, timeZone: TimeZone = .current) -> String {
        UiMessages.string(detailText, locale: locale, timeZone: timeZone)
    }

    public static func == (lhs: NativeRouteDestinationRow, rhs: NativeRouteDestinationRow) -> Bool {
        lhs.icon == rhs.icon && lhs.title == rhs.title && lhs.detail == rhs.detail
            && lhs.declaredLanguage == rhs.declaredLanguage && lhs.detectedLanguage == rhs.detectedLanguage
            && lhs.detailDeclaredLanguage == rhs.detailDeclaredLanguage
            && lhs.detailDetectedLanguage == rhs.detailDetectedLanguage
            && lhs.externalURL == rhs.externalURL && lhs.targetPath == rhs.targetPath
    }

    public func hash(into hasher: inout Hasher) {
        hasher.combine(icon)
        hasher.combine(title)
        hasher.combine(detail)
        hasher.combine(declaredLanguage)
        hasher.combine(detectedLanguage)
        hasher.combine(detailDeclaredLanguage)
        hasher.combine(detailDetectedLanguage)
        hasher.combine(externalURL)
        hasher.combine(targetPath)
    }
}
