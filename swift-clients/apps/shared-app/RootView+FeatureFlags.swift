import VouchaAPI
import VouchaFeatures
import VouchaModels

extension RootView {
    func refreshFeatureFlags(retryUntilSuccess: Bool = true) async {
        var retryDelaySeconds = 5
        while !Task.isCancelled {
            do {
                let fetchToken = featureFlagState.beginPublicFetch()
                let response: FeatureFlagsResponse = try await viewModelFactory.apiClient.send(.featureFlags)
                featureFlagState.applyPublicResponse(response, for: fetchToken)
                routePendingFeatureFlagURLIfVisible()
                return
            } catch {
                guard retryUntilSuccess, !featureFlagState.hasLoadedRemote else { return }
                try? await Task.sleep(for: .seconds(retryDelaySeconds))
                retryDelaySeconds = min(retryDelaySeconds * 2, 60)
            }
        }
    }

    func routePendingFeatureFlagURLIfVisible() {
        guard let url = pendingFeatureFlagRouteURL else { return }
        guard featureFlagState.effective["fediverse"] == true else {
            pendingFeatureFlagRouteURL = nil
            return
        }
        pendingFeatureFlagRouteURL = nil
        routeNativeURL(url)
    }

    func featureFlagsDidChange() {
        routePendingFeatureFlagURLIfVisible()
        NativeFeatureFlagRouteInvalidation.clearIfNeeded(
            entry: &selectedNativeRouteEntry,
            match: &selectedNativeRouteMatch,
            query: &selectedNativeRouteQuery,
            featureFlags: featureFlagState.effective
        )
    }
}
