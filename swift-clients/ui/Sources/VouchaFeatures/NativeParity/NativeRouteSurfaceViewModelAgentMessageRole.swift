import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func agentMessageRole(_ createdById: String?, agentSystemUserId: String?) -> UiVerbatimText {
        guard let createdById else {
            return appText(.nativeSwiftPresentationValuesDeleted)
        }

        return createdById == agentSystemUserId
            ? appText(.nativeSwiftRouteSurfaceAgent)
            : appText(.nativeSwiftRouteSurfaceAgentUser)
    }
}
