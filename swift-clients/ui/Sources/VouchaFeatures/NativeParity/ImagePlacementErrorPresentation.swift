import VouchaCore
import VouchaLocalization

enum ImagePlacementErrorPresentation {
    static func message(for error: Error) -> UiVerbatimText? {
        guard let error = error as? VouchaError else { return nil }
        switch error {
        case let .api(_, code), let .apiMessage(_, code, _):
            guard code == "IMAGE_PLACEMENT_LIMIT_REACHED" else { return nil }
            return .app(UiMessage(.nativeCommonImagePlacementLimitReached))
        default:
            return nil
        }
    }
}

enum NativeContributionAdmissionPresentation {
    static func messageKey(_ code: String?) -> UiMessageKey? {
        switch code {
        case "CONTRIBUTION_ADMISSION_IN_PROGRESS": .nativeTaxonomyContributionAdmissionInProgress
        case "CONTRIBUTION_QUOTA_EXCEEDED": .nativeTaxonomyContributionAdmissionCapacityUnavailable
        case "IDEMPOTENCY_KEY_REUSED": .nativeTaxonomyContributionAdmissionIdempotencyMismatch
        default: nil
        }
    }
}
