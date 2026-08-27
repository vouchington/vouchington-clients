import Foundation
import VouchaAPI

struct TrimmedLandingPageMetadata {
    let title: String
    let subtitlePatch: NullableStringPatchField
    let slug: String
}

func trimmedMetadata(title: String, subtitle: String, slug: String) -> TrimmedLandingPageMetadata {
    let trimmedSubtitle = subtitle.trimmingCharacters(in: .whitespacesAndNewlines)
    return TrimmedLandingPageMetadata(
        title: title.trimmingCharacters(in: .whitespacesAndNewlines),
        subtitlePatch: trimmedSubtitle.isEmpty ? .null : .value(trimmedSubtitle),
        slug: slug.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    )
}

func sortedReplacing(page: LandingPage, in pages: [LandingPage]) -> [LandingPage] {
    let replaced = pages.contains { $0.id == page.id }
        ? pages.map { $0.id == page.id ? page : $0 }
        : pages + [page]
    return replaced.sorted {
        if $0.isDefault != $1.isDefault {
            return $0.isDefault && !$1.isDefault
        }
        return $0.createdAt < $1.createdAt
    }
}

func pageWithDefault(_ page: LandingPage, isDefault: Bool) -> LandingPage {
    LandingPage(
        id: page.id,
        userId: page.userId,
        title: page.title,
        subtitle: page.subtitle,
        slug: page.slug,
        isDefault: isDefault,
        createdAt: page.createdAt,
        updatedAt: page.updatedAt
    )
}
