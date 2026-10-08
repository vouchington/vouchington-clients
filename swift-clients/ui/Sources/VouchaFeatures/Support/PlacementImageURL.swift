import VouchaCore
import VouchaModels

extension AppConfig {
    func imageURL(for placement: ImagePlacement?, width: Int = 96) -> String? {
        guard let placement else { return nil }
        return imageURL(
            forPlacementId: placement.placementId,
            revision: placement.placementRevision,
            imageId: placement.imageId,
            width: width
        )
    }
}
