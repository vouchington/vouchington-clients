import VouchaAPI
import VouchaFeatures
import VouchaLocalization

extension RootView {
    func refreshLocalization() async {
        await LocalizationRefreshService.refresh(
            client: viewModelFactory.apiClient,
            controller: viewModelFactory.uiLocaleController
        )
    }
}
