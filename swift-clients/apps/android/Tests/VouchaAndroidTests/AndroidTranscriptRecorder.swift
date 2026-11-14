import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

final class AndroidTranscriptRecorder: @unchecked Sendable {
    private struct RequestWaiter {
        let path: String
        let occurrence: Int
        let continuation: CheckedContinuation<URLRequest, Error>
    }

    private struct ResolvedRequestWaiter {
        let id: UUID
        let continuation: CheckedContinuation<URLRequest, Error>
        let request: URLRequest
    }

    private struct RequestStartResult {
        let plan: AndroidTranscriptURLProtocol.ResponsePlan
        let occurrence: Int
        let resolvedWaiters: [ResolvedRequestWaiter]
    }

    private enum WaitState {
        case starting
        case waiting
        case cancelled
        case finished
    }

    private let lock = NSLock()
    private var plans: [String: [AndroidTranscriptURLProtocol.ResponsePlan]] = [:]
    private var requests: [String: [URLRequest]] = [:]
    private var held: [String: [Int: AndroidTranscriptURLProtocol]] = [:]
    private var waitStates: [UUID: WaitState] = [:]
    private var waiters: [UUID: RequestWaiter] = [:]

    func enqueue(path: String, statusCode: Int = 200, data: Data, held: Bool = false) {
        lock.withLock {
            plans[path, default: []].append(.init(
                statusCode: statusCode,
                data: data,
                held: held,
                responseBuilder: nil
            ))
        }
    }

    func enqueue(
        path: String,
        statusCode: Int = 200,
        held: Bool = false,
        responseBuilder: @escaping @Sendable (URLRequest) -> Data
    ) {
        lock.withLock {
            plans[path, default: []].append(.init(
                statusCode: statusCode,
                data: Data(),
                held: held,
                responseBuilder: responseBuilder
            ))
        }
    }

    func start(
        _ request: URLRequest,
        owner: AndroidTranscriptURLProtocol
    ) -> (AndroidTranscriptURLProtocol.ResponsePlan, Int) {
        let path = request.url?.path ?? ""
        let result = lock.withLock { () -> RequestStartResult in
            requests[path, default: []].append(request)
            let occurrence = requests[path, default: []].count
            var queued = plans[path] ?? []
            let plan = queued.isEmpty
                ? .init(
                    statusCode: 500,
                    data: Data(#"{"message":"Unexpected request"}"#.utf8),
                    held: false,
                    responseBuilder: nil
                )
                : queued.removeFirst()
            plans[path] = queued
            if plan.held { held[path, default: [:]][occurrence] = owner }
            let completedWaiters = waiters.compactMap { id, waiter -> ResolvedRequestWaiter? in
                guard waiter.path == path, waiter.occurrence > 0, waiter.occurrence <= occurrence else { return nil }
                let request = requests[path, default: []][waiter.occurrence - 1]
                return ResolvedRequestWaiter(id: id, continuation: waiter.continuation, request: request)
            }
            for waiter in completedWaiters {
                waiters.removeValue(forKey: waiter.id)
                waitStates.removeValue(forKey: waiter.id)
            }
            return RequestStartResult(plan: plan, occurrence: occurrence, resolvedWaiters: completedWaiters)
        }
        for waiter in result.resolvedWaiters {
            waiter.continuation.resume(returning: waiter.request)
        }
        return (result.plan, result.occurrence)
    }

    func completeHeld(path: String, occurrence: Int, statusCode: Int, data: Data) {
        let owner = lock.withLock { held[path]?.removeValue(forKey: occurrence) }
        owner?.deliver(statusCode: statusCode, data: data)
    }

    func requestSnapshot(path: String? = nil) -> [URLRequest] {
        lock.withLock {
            if let path { return requests[path] ?? [] }
            return requests.values.flatMap { $0 }
        }
    }

    enum WaitError: Error {
        case requestTimedOut(path: String, occurrence: Int)
        case invalidWaitState
    }
}

extension AndroidTranscriptRecorder {
    func waitForRequest(path: String, occurrence: Int) async throws -> URLRequest {
        let id = UUID()
        lock.withLock { waitStates[id] = .starting }
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                let result = lock.withLock { () -> (WaitState, URLRequest?) in
                    switch waitStates[id] {
                    case .cancelled:
                        waitStates.removeValue(forKey: id)
                        return (.cancelled, nil)
                    default:
                        let pathRequests = requests[path] ?? []
                        if pathRequests.count >= occurrence {
                            waitStates.removeValue(forKey: id)
                            return (.finished, pathRequests[occurrence - 1])
                        }
                        waitStates[id] = .waiting
                        waiters[id] = RequestWaiter(path: path, occurrence: occurrence, continuation: continuation)
                        return (.waiting, nil)
                    }
                }
                switch result.0 {
                case .cancelled:
                    continuation.resume(throwing: CancellationError())
                case .finished:
                    if let request = result.1 { continuation.resume(returning: request) }
                case .waiting:
                    Task {
                        try? await Task.sleep(for: .seconds(5))
                        timeoutWaiter(id, path: path, occurrence: occurrence)
                    }
                case .starting:
                    continuation.resume(throwing: WaitError.invalidWaitState)
                }
            }
        } onCancel: {
            cancelWaiter(id)
        }
    }

    private func cancelWaiter(_ id: UUID) {
        let continuation = lock.withLock { () -> CheckedContinuation<URLRequest, Error>? in
            switch waitStates[id] {
            case .starting:
                waitStates[id] = .cancelled
                return nil
            case .waiting:
                let waiter = waiters.removeValue(forKey: id)
                waitStates.removeValue(forKey: id)
                return waiter?.continuation
            case .cancelled, .finished, nil:
                return nil
            }
        }
        continuation?.resume(throwing: CancellationError())
    }

    private func timeoutWaiter(_ id: UUID, path: String, occurrence: Int) {
        let continuation = lock.withLock { () -> CheckedContinuation<URLRequest, Error>? in
            guard case .waiting = waitStates[id],
                  let waiter = waiters.removeValue(forKey: id)
            else { return nil }
            waitStates.removeValue(forKey: id)
            return waiter.continuation
        }
        continuation?.resume(throwing: WaitError.requestTimedOut(path: path, occurrence: occurrence))
    }

}
