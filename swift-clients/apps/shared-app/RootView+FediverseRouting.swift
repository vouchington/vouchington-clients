import Foundation
import VouchaFeatures

extension RootView {
    func allowsNativeRouteFeature(
        destination: NativeRouteDestinationIdentifier,
        routeMatch: NativeRouteMatch,
        url: URL
    ) -> Bool {
        guard !shouldQueueUntilFeatureFlagsLoad(
            destination: destination,
            routeMatch: routeMatch,
            url: url
        ) else { return false }
        return !routeMatch.requiresFediverseFeature || featureFlagState.effective["fediverse"] == true
    }

    func shouldQueueUntilFeatureFlagsLoad(
        destination: NativeRouteDestinationIdentifier,
        routeMatch: NativeRouteMatch,
        url: URL
    ) -> Bool {
        guard destination.isFediverseDestination || routeMatch.requiresFediverseFeature,
              featureFlagState.effective["fediverse"] != true,
              !featureFlagState.hasLoadedRemote
        else {
            return false
        }
        pendingFeatureFlagRouteURL = url
        Task {
            await refreshFeatureFlags()
        }
        return true
    }
}
