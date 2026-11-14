import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization

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

extension LandingPagesViewModel {
    func requireClient() -> APIClient? {
        guard let client else {
            handle(LandingPagesViewModelError.missingClient)
            return nil
        }
        return client
    }

    func handle(_ error: Error) {
        if let error = error as? VouchaError {
            switch error {
            case let .api(_, code), let .apiMessage(_, code, _):
                isItemLimitReached = code == "LANDING_PAGE_ITEM_LIMIT_REACHED"
            default: isItemLimitReached = false
            }
            if isItemLimitReached {
                state = .error(error)
                errorMessage = .protocolValue(error.localizedDescription)
                return
            }
            state = .error(error)
            errorMessage = .verbatim(String(describing: error))
        } else {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            errorMessage = .verbatim(error.localizedDescription)
        }
    }
}

enum LandingPagesViewModelError: LocalizedError {
    case missingClient

    var errorDescription: String? {
        "Landing pages are unavailable because the API client is not configured."
    }
}
