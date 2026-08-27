import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public enum EmailAddressManagerPhase: Equatable, Sendable {
    case enterEmail
    case enterCode
    case verified
}

@Observable
@MainActor
public final class EmailAddressManagerViewModel {
    var pagination = CursorPaginationState<EmailAddress>()
    public var emailAddresses: [EmailAddress] {
        pagination.items
    }

    public var emailAddress = ""
    public var verificationCode = ""
    public internal(set) var phase: EmailAddressManagerPhase = .enterEmail
    public internal(set) var isLoading = false
    public internal(set) var errorMessage: UiVerbatimText?
    public internal(set) var statusMessage: UiMessage?
    private let client: APIClient

    public init(client: APIClient) {
        self.client = client
    }

    public func load() async {
        await perform {
            let response: EmailAddressListResponse = try await client.send(.myEmailAddresses())
            pagination.reset(items: response.results)
            pagination.restoreContinuation(
                endCursor: response.pageInfo.endCursor,
                hasMore: response.pageInfo.hasNextPage
            )
        }
    }

    public func requestVerification() async {
        await perform {
            let response: EmailAddressVerificationRequestResponse = try await client.send(
                .requestMyEmailAddressVerification(emailAddress: emailAddress)
            )
            emailAddress = response.emailAddress
            verificationCode = ""
            phase = .enterCode
            statusMessage = UiMessage(.nativeSwiftSettingsVerificationCodeSent)
        }
    }

    public func verify() async {
        await perform {
            let response: EmailAddressListResponse = try await client.send(
                .verifyMyEmailAddress(emailAddress: emailAddress, token: verificationCode)
            )
            pagination.reset(items: response.results)
            pagination.restoreContinuation(
                endCursor: response.pageInfo.endCursor,
                hasMore: response.pageInfo.hasNextPage
            )
            phase = .verified
            statusMessage = UiMessage(.nativeSwiftSettingsEmailVerifiedRetry)
        }
    }

    public func startAnotherAddress() {
        emailAddress = ""
        verificationCode = ""
        phase = .enterEmail
        errorMessage = nil
        statusMessage = nil
    }

    public func loadMore() async {
        guard let request = pagination.beginNextPage() else { return }
        do {
            let response: EmailAddressListResponse = try await client.send(
                .myEmailAddresses(after: request.cursor)
            )
            pagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            pagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                pagination.cancel(request)
                return
            }
            pagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                pagination.cancel(request)
                return
            }
            pagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    private func perform(_ operation: () async throws -> Void) async {
        guard !isLoading else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            try await operation()
        } catch is CancellationError {
            return
        } catch let error as VouchaError {
            if Task.isCancelled {
                return
            }
            errorMessage = .verbatim(error.errorDescription ?? "")
        } catch {
            if Task.isCancelled {
                return
            }
            errorMessage = .verbatim(error.localizedDescription)
        }
    }
}
