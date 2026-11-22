import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativePostComposeViewModel {
    /// Fetches the configured image cap. On failure the composer stays uncapped and the server's
    /// validation decides. A response superseded by a newer load or by leaving the surface is dropped.
    func loadImageLimit() async {
        guard let client else { return }
        imageLimitGeneration += 1
        let generation = imageLimitGeneration
        let limit: Int?
        do {
            let limits: ContentLimitsResponse = try await client.send(.contentLimits())
            limit = limits.postImagesMax
        } catch {
            limit = nil
        }
        guard generation == imageLimitGeneration else { return }
        imageLimit = limit
    }

    /// Only shown when the local cap blocks an upload, so `imageLimit` is set in practice.
    var imageLimitReachedMessage: UiVerbatimText {
        .app(UiMessage(.sharedPostImageLimitMax, numberParameters: ["max": Double(imageLimit ?? images.count)]))
    }
}
