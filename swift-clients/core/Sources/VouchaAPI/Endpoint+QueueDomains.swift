private struct EngineeringPauseDomainBody: Encodable {
    let reason: String
}

public extension Endpoint {
    static func mqQueueDomains(name: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/mq/queues/\(pathSegment(name))/domains")
    }

    static func mqPauseDomain(queue: String, domain: String, reason: String? = nil) -> Endpoint {
        let path = queueDomainPath(queue: queue, domain: domain) + "/pause"
        if let reason {
            return Endpoint(.POST, path: path, body: EngineeringPauseDomainBody(reason: reason))
        }
        return Endpoint(.POST, path: path)
    }

    static func mqResumeDomain(queue: String, domain: String) -> Endpoint {
        Endpoint(.POST, path: queueDomainPath(queue: queue, domain: domain) + "/resume")
    }

    static func mqRetryFailedDomain(queue: String, domain: String) -> Endpoint {
        Endpoint(.POST, path: queueDomainPath(queue: queue, domain: domain) + "/retry-failed")
    }

    private static func queueDomainPath(queue: String, domain: String) -> String {
        "/api/v1/mq/queues/\(pathSegment(queue))/domains/\(pathSegment(domain))"
    }
}
