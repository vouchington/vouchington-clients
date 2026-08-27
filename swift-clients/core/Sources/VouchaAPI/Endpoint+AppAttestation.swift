public extension Endpoint {
    static func appAttestationChallenge(type: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/app-attestation/challenge", body: ["type": type])
    }

    static func appAttestationAttest(keyId: String, attestation: String, challengeId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/app-attestation/attest",
            body: ["keyId": keyId, "attestation": attestation, "challengeId": challengeId]
        )
    }
}
