public extension NativeRouteFamilyGroup {
    func isVisible(isSignedIn _: Bool, userRoles: [String]) -> Bool {
        requiredRoles.isEmpty || requiredRoles.contains { userRoles.contains($0) }
    }
}

public extension AppSection {
    func nativeParityGroups(isSignedIn: Bool, userRoles: [String]) -> [NativeRouteFamilyGroup] {
        nativeParityGroups(isSignedIn: isSignedIn, userRoles: userRoles, featureFlags: [:])
    }

    func nativeParityGroups(
        isSignedIn: Bool,
        userRoles: [String],
        featureFlags: [String: Bool]
    ) -> [NativeRouteFamilyGroup] {
        guard isVisible(isSignedIn: isSignedIn, userRoles: userRoles) else {
            return []
        }
        return nativeParityGroups(featureFlags: featureFlags)
            .filter { $0.isVisible(isSignedIn: isSignedIn, userRoles: userRoles) }
    }
}
