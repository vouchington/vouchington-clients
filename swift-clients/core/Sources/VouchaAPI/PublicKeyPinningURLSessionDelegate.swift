import Foundation

#if canImport(Security)
    import CryptoKit
    import Security

    final class PublicKeyPinningURLSessionDelegate: NSObject, URLSessionDelegate {
        private let policy: PublicKeyPinningPolicy

        init(policy: PublicKeyPinningPolicy) {
            self.policy = policy
        }

        func urlSession(
            _: URLSession,
            didReceive challenge: URLAuthenticationChallenge,
            completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
        ) {
            guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
                  let trust = challenge.protectionSpace.serverTrust
            else {
                completionHandler(.performDefaultHandling, nil)
                return
            }

            let host = challenge.protectionSpace.host
            guard policy.isEligibleHost(host) else {
                completionHandler(.performDefaultHandling, nil)
                return
            }

            var error: CFError?
            guard SecTrustEvaluateWithError(trust, &error) else {
                completionHandler(.cancelAuthenticationChallenge, nil)
                return
            }

            guard let spkiHash = Self.leafSPKISHA256Base64(from: trust) else {
                completionHandler(.cancelAuthenticationChallenge, nil)
                return
            }

            switch policy.validate(host: host, spkiSha256Base64: spkiHash) {
            case .accepted, .notPinned:
                completionHandler(.useCredential, URLCredential(trust: trust))
            case .rejected:
                completionHandler(.cancelAuthenticationChallenge, nil)
            }
        }

        static func leafSPKISHA256Base64(from trust: SecTrust) -> String? {
            guard let certificate = (SecTrustCopyCertificateChain(trust) as? [SecCertificate])?.first,
                  let publicKey = SecCertificateCopyKey(certificate),
                  let spki = spkiDERData(from: publicKey)
            else {
                return nil
            }
            return Data(SHA256.hash(data: spki)).base64EncodedString()
        }

        private static func spkiDERData(from publicKey: SecKey) -> Data? {
            guard let attributes = SecKeyCopyAttributes(publicKey) as? [String: Any],
                  let keyType = attributes[kSecAttrKeyType as String] as? String,
                  let keySize = attributes[kSecAttrKeySizeInBits as String] as? Int
            else {
                return nil
            }
            var error: Unmanaged<CFError>?
            guard let keyData = SecKeyCopyExternalRepresentation(publicKey, &error) as Data? else {
                return nil
            }
            guard let header = spkiHeader(keyType: keyType, keySize: keySize) else {
                return nil
            }
            return header + keyData
        }
    }
#endif
