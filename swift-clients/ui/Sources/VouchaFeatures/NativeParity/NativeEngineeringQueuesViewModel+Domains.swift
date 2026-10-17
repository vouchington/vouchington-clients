import VouchaAPI

extension NativeEngineeringQueuesViewModel {
    func pauseDomain(_ domain: String, reason: String? = nil) async {
        await performAction(key: "domain|\(domain)") {
            let _: EngineeringSuccessResponse = try await self.client.send(
                .mqPauseDomain(queue: "background", domain: domain, reason: reason)
            )
        }
    }

    func resumeDomain(_ domain: String) async {
        await performAction(key: "domain|\(domain)") {
            let _: EngineeringSuccessResponse = try await self.client.send(
                .mqResumeDomain(queue: "background", domain: domain)
            )
        }
    }

    func retryFailedDomain(_ domain: String) async {
        await performAction(key: "domain|\(domain)") {
            let _: EngineeringRetryDomainResponse = try await self.client.send(
                .mqRetryFailedDomain(queue: "background", domain: domain)
            )
        }
    }
}
