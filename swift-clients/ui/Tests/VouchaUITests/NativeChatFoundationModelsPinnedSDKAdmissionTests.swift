import CryptoKit
import Foundation
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    private struct MeasuredDescriptorTool: Tool {
        let name: String
        let description: String
        let parameters: GenerationSchema
        func call(arguments _: GeneratedContent) async throws -> String {
            ""
        }
    }

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsPinnedSDKAdmissionTests: XCTestCase {
        func testPinnedSDKMeasuresAuthorizedDescriptorsAndContextAdmission() async throws {
            executionTimeAllowance = 1_000
            if ProcessInfo.processInfo.environment["VOUCHA_MCP_ADMISSION_PROBE"] == "1" {
                try await runIsolatedProbe()
                return
            }
            let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
            let descriptors = try Self.authorizedDescriptors(root: root)
            let names = descriptors.map(\.name)
            XCTAssertEqual(Set(names).count, names.count)
            XCTAssertGreaterThan(names.count, NativeChatFoundationModelsToolSelection.maximumTools)
            XCTAssertTrue(names.contains("read_my_profile"))
            XCTAssertTrue(names.contains("read_topic"))
            let directory = FileManager.default.temporaryDirectory.appendingPathComponent(
                "voucha-mcp-admission-\(UUID().uuidString)", isDirectory: true
            )
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let journal = ProbeJournal(url: directory.appendingPathComponent("journal.jsonl"))
            FileManager.default.createFile(atPath: journal.url.path, contents: Data())
            var skips: [String] = []
            var hostFailures: [[String: Any]] = []
            for launch in 1 ... 6 {
                let child = try launchProbe(
                    phase: "descriptors", skip: skips, journal: journal, root: root, directory: directory,
                    timeout: 720
                )
                if child.timedOut {
                    XCTFail("Descriptor probe timed out. Production caps stay unchanged. \(child.tail)")
                    return
                }
                if let signal = child.signal {
                    let current = try journal.lines().last
                    guard current?["status"] as? String == "counting", let name = current?["name"] as? String,
                          !skips.contains(name) else {
                        XCTFail(
                            "Signal \(signal) did not name a new descriptor. Record it as a host SDK failure. " +
                                "Production caps stay unchanged. \(child.tail)"
                        )
                        return
                    }
                    skips.append(name)
                    hostFailures.append([
                        "server": current?["server"] as? String ?? "", "name": name, "status": "hostSDKFailure",
                        "signal": Int(signal), "phase": current?["phase"] as? String ?? "counting"
                    ])
                    if launch == 6 {
                        XCTFail("Descriptor measurement stopped after repeated host signals: \(skips).")
                        return
                    }
                    continue
                }
                XCTAssertEqual(child.exitCode, 0, child.tail)
                break
            }
            let admissionChild = try launchProbe(
                phase: "admission", skip: [], journal: journal, root: root, directory: directory, timeout: 240
            )
            var records = try journal.latestByName()
            for failure in hostFailures {
                if let name = failure["name"] as? String { records[name] = failure }
            }
            let admission = try journal.latestAdmission()
            let catalog = try Data(contentsOf: FilamentsContractRoot.requiredURL(
                "api-fixtures/v1/mcp.json",
                root: root
            ))
            let digest = SHA256.hash(data: catalog).map { String(format: "%02x", $0) }.joined()
            let versionLine = Self.command("/usr/bin/xcrun", ["xcodebuild", "-version"])?.split(separator: "\n").first
            let xcode = versionLine?.split(separator: " ").dropFirst().first.map(String.init)
            let report: [String: Any] = [
                "xcode": xcode ?? "unknown",
                "nativeRevision": Self.nativeRevision() ?? "",
                "producerGitRevision": Self.command("/usr/bin/git", ["-C", root.path, "rev-parse", "HEAD"]) ?? "",
                "producerManifestRevision": Self.producerManifestSHA(root: root) ?? "",
                "catalogSHA256": digest,
                "minimumAnswerHeadroom": NativeChatModelContextAdmission.minimumAnswerHeadroom,
                "descriptors": records.values
                    .sorted { String(describing: $0["name"]) < String(describing: $1["name"]) },
                "admission": admission ?? [:],
                "hostFailureMeaning": "A tokenizer signal is a host SDK failure. Production caps stay unchanged."
            ]
            let payload = try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
            print("MCP_ADMISSION_MEASUREMENT \(String(decoding: payload, as: UTF8.self))")
            XCTAssertEqual(Set(records.keys), Set(names))
            XCTAssertFalse((report["nativeRevision"] as? String ?? "").isEmpty)
            XCTAssertTrue(
                (report["producerGitRevision"] as? String ?? "").isEmpty == false ||
                    (report["producerManifestRevision"] as? String ?? "").isEmpty == false
            )
            var blocked: [String] = []
            for (name, record) in records {
                switch record["status"] as? String {
                case "measured":
                    XCTAssertNotNil(Self.jsonInt(record["schemaTokens"]), name)
                    XCTAssertNotNil(Self.jsonInt(record["toolTokens"]), name)
                case "policyExclusion":
                    XCTAssertEqual(record["reason"] as? String, "projectionUnsupported", name)
                case "hostSDKFailure":
                    XCTAssertNotNil(Self.jsonInt(record["signal"]), name)
                default:
                    blocked.append(name)
                }
            }
            XCTAssertTrue(blocked.isEmpty, "Unexpected descriptor results: \(blocked)")
            let pinned = xcode == "26.6"
            if pinned {
                let signaled = records.filter { $0.value["status"] as? String == "hostSDKFailure" }
                XCTAssertTrue(
                    signaled.isEmpty,
                    "Pinned Xcode 26.6 must count every projected descriptor. A signal is a host SDK failure. " +
                        "Production caps stay unchanged. \(signaled)"
                )
            }
            try assertAdmission(admission, child: admissionChild, pinned: pinned)
        }

        private func assertAdmission(_ admission: [String: Any]?, child: ProbeChild, pinned: Bool) throws {
            if child.timedOut {
                XCTFail("Admission probe timed out. Production caps stay unchanged. \(child.tail)")
                return
            }
            if child.signal != nil {
                XCTAssertFalse(
                    pinned,
                    "Pinned Xcode 26.6 admission counting ended with signal \(child.signal ?? -1). " +
                        "Record it as a host SDK failure and leave production caps unchanged. \(child.tail)"
                )
                return
            }
            XCTAssertEqual(child.exitCode, 0, child.tail)
            let record = try XCTUnwrap(admission)
            XCTAssertEqual(record["status"] as? String, "measured", String(describing: record))
            XCTAssertEqual(Self.jsonInt(record["headroom"]), NativeChatModelContextAdmission.minimumAnswerHeadroom)
            let contextSize = try XCTUnwrap(Self.jsonInt(record["contextSize"]))
            let headroom = try XCTUnwrap(Self.jsonInt(record["headroom"]))
            XCTAssertGreaterThan(contextSize, headroom)
            XCTAssertGreaterThan(
                try XCTUnwrap(Self.jsonInt(record["overflowBytes"])),
                try XCTUnwrap(Self.jsonInt(record["fittingBytes"]))
            )
            XCTAssertEqual(record["fittingRetained"] as? Bool, true)
            XCTAssertEqual(record["overflowRetained"] as? Bool, false)
            XCTAssertEqual(record["prepareStoppedWhenFinalFits"] as? Bool, true)
            XCTAssertEqual(record["finalTranscriptFits"] as? Bool, true)
        }

        private func launchProbe(
            phase: String, skip: [String], journal: ProbeJournal, root: URL, directory: URL, timeout: Int
        ) throws -> ProbeChild {
            let bundle = Bundle(for: Self.self).bundleURL
            guard bundle.pathExtension == "xctest" else { throw ProbeError.bundle(bundle.path) }
            let logURL = directory.appendingPathComponent("probe-\(phase)-\(UUID().uuidString).log")
            FileManager.default.createFile(atPath: logURL.path, contents: Data())
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/usr/bin/xcrun")
            process.arguments = [
                "xctest", "-XCTest",
                "NativeChatFoundationModelsPinnedSDKAdmissionTests/" +
                    "testPinnedSDKMeasuresAuthorizedDescriptorsAndContextAdmission",
                bundle.path
            ]
            var environment = ProcessInfo.processInfo.environment
            environment["VOUCHA_MCP_ADMISSION_PROBE"] = "1"
            environment["VOUCHA_MCP_ADMISSION_JOURNAL"] = journal.url.path
            environment["VOUCHA_MCP_ADMISSION_PHASE"] = phase
            environment["VOUCHA_MCP_ADMISSION_SKIP"] = skip.joined(separator: ",")
            environment[FilamentsContractRoot.environmentKey] = root.path
            process.environment = environment
            let log = try FileHandle(forWritingTo: logURL)
            process.standardOutput = log
            process.standardError = log
            let group = DispatchGroup()
            group.enter()
            process.terminationHandler = { _ in group.leave() }
            do { try process.run() } catch {
                group.leave()
                throw error
            }
            let finished = group.wait(timeout: .now() + .seconds(timeout)) == .success
            if !finished {
                process.terminate()
                _ = group.wait(timeout: .now() + .seconds(15))
            }
            try log.close()
            let output = (try? String(contentsOf: logURL, encoding: .utf8)) ?? ""
            let signal = finished && process.terminationReason == .uncaughtSignal ? process.terminationStatus : nil
            return ProbeChild(
                exitCode: process.terminationStatus,
                signal: signal,
                timedOut: !finished,
                tail: Self.scrub(output)
            )
        }

        private func runIsolatedProbe() async throws {
            guard let journalPath = ProcessInfo.processInfo.environment["VOUCHA_MCP_ADMISSION_JOURNAL"],
                  let phase = ProcessInfo.processInfo.environment["VOUCHA_MCP_ADMISSION_PHASE"] else {
                throw ProbeError.missingEnvironment
            }
            let journal = ProbeJournal(url: URL(fileURLWithPath: journalPath))
            if phase == "admission" {
                try await measureAdmission(journal: journal)
                return
            }
            let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
            let skip = Set((ProcessInfo.processInfo.environment["VOUCHA_MCP_ADMISSION_SKIP"] ?? "")
                .split(separator: ",").map(String.init))
            let done = try Set(journal.latestByName().filter {
                ["measured", "policyExclusion", "projectionFailure", "sdkError"].contains($0.value["status"] as? String)
            }.keys)
            for descriptor in try Self.authorizedDescriptors(root: root)
                where !skip.contains(descriptor.name) && !done.contains(descriptor.name) {
                try await measure(descriptor, journal: journal)
            }
        }

        private func measure(_ descriptor: AuthorizedDescriptor, journal: ProbeJournal) async throws {
            let name = descriptor.name
            func mark(_ fields: [String: Any]) throws {
                var record = fields
                record["server"] = descriptor.server
                record["name"] = name
                try journal.append(record)
            }
            try mark(["status": "counting", "phase": "project"])
            let schema: GenerationSchema
            do { schema = try NativeChatFoundationModelsSchema.project(descriptor.parameters) }
            catch NativeChatFoundationModelsSchema.Failure.unsupported {
                try mark(["status": "policyExclusion", "reason": "projectionUnsupported"])
                return
            } catch {
                try mark(["status": "projectionFailure", "message": String(describing: error)])
                return
            }
            let model = SystemLanguageModel.default
            do {
                try mark(["status": "counting", "phase": "schema"])
                let schemaTokens = try await model.tokenCount(for: schema)
                let tool = MeasuredDescriptorTool(name: name, description: descriptor.description, parameters: schema)
                try mark(["status": "counting", "phase": "tool", "schemaTokens": schemaTokens])
                let toolTokens = try await model.tokenCount(for: [tool])
                try mark(["status": "measured", "schemaTokens": schemaTokens, "toolTokens": toolTokens])
            } catch {
                try mark(["status": "sdkError", "message": String(describing: error)])
            }
        }

        private func measureAdmission(journal: ProbeJournal) async throws {
            try journal.append(["phase": "admission", "status": "counting"])
            do {
                let record = try await Self.admissionRecord()
                try journal.append(record)
            } catch {
                try journal.append(["phase": "admission", "status": "probeError", "message": String(describing: error)])
            }
        }

        private static func admissionRecord() async throws -> [String: Any] {
            let contextSize = SystemLanguageModel.default.contextSize
            let headroom = NativeChatModelContextAdmission.minimumAnswerHeadroom
            let call = try Transcript.ToolCall(
                id: "pinned-sdk-call", toolName: "read_my_profile", arguments: GeneratedContent(json: "{}")
            )
            let baseline: [Transcript.Entry] = [.toolCalls(.init([call]))]
            let boundary = try await tokenBoundary { length in
                try await retained(baseline, envelope: String(repeating: "a", count: length))
            }
            let fitting = String(repeating: "a", count: boundary.fitting)
            let overflow = String(repeating: "a", count: boundary.overflow)
            let fittingRetained = try await retained(baseline, envelope: fitting)
            let overflowRetained = try await retained(baseline, envelope: overflow)
            guard fittingRetained, overflowRetained == false, overflow.utf8.count > fitting.utf8.count else {
                throw ProbeError.boundaryMismatch
            }
            let final = try await finalWindow(call: call)
            return [
                "phase": "admission", "status": "measured", "contextSize": contextSize, "headroom": headroom,
                "fittingBytes": fitting.utf8.count, "overflowBytes": overflow.utf8.count,
                "fittingRetained": fittingRetained, "overflowRetained": overflowRetained,
                "finalTranscriptTokens": final.tokens, "prepareStoppedWhenFinalFits": final.prepareStopped,
                "finalTranscriptFits": final.finalFits
            ]
        }

        private static func retained(_ baseline: [Transcript.Entry], envelope: String) async throws -> Bool {
            let session = LanguageModelSession(transcript: Transcript(entries: baseline))
            let admission = NativeChatModelContextAdmission()
            admission.session = session
            let callID = try await admission.prepare(name: "read_my_profile", arguments: "{}")
            return try await admission.retain(callId: callID, name: "read_my_profile", envelope: envelope)
        }

        private static func finalWindow(
            call: Transcript.ToolCall
        ) async throws -> (tokens: Int, prepareStopped: Bool, finalFits: Bool) {
            let model = SystemLanguageModel.default
            func outcome(_ length: Int) async throws -> (tokens: Int, prepareStopped: Bool, finalFits: Bool) {
                let text = String(repeating: "a", count: length)
                let entries: [Transcript.Entry] = [
                    .prompt(.init(segments: [.text(.init(content: text))])), .toolCalls(.init([call]))
                ]
                let tokens = try await model.tokenCount(for: entries)
                let session = LanguageModelSession(transcript: Transcript(entries: entries))
                let admission = NativeChatModelContextAdmission()
                admission.session = session
                let prepareStopped: Bool
                do {
                    _ = try await admission.prepare(name: "read_my_profile", arguments: "{}")
                    prepareStopped = false
                } catch NativeChatModelContextAdmission.Failure.contextUnavailable {
                    prepareStopped = true
                }
                let finalFits = try await admission.finalTranscriptFits()
                return (tokens, prepareStopped, finalFits)
            }
            let boundary = try await tokenBoundary { try await outcome($0).finalFits }
            let crowded = try await outcome(boundary.fitting)
            guard crowded.finalFits, crowded.prepareStopped else { throw ProbeError.headroomWindowMissed }
            return crowded
        }

        private static func tokenBoundary(fits: (Int) async throws -> Bool) async throws
            -> (fitting: Int, overflow: Int) {
            var low = 0
            var high = 1
            var steps = 0
            while try await fits(high) {
                steps += 1
                if steps > 24 || high > 1_000_000 { throw ProbeError.overflowUnreachable }
                low = high
                high *= 2
            }
            var best = low
            var left = low
            var right = high
            while left <= right {
                let mid = left + (right - left) / 2
                if try await fits(mid) {
                    best = mid
                    left = mid + 1
                } else { right = mid - 1 }
            }
            guard try await fits(best), try await fits(best + 1) == false else { throw ProbeError.boundaryMismatch }
            return (best, best + 1)
        }

        private static func authorizedDescriptors(root: URL) throws -> [AuthorizedDescriptor] {
            let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/mcp.json", root: root)
            let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
            let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
            var descriptors: [AuthorizedDescriptor] = []
            for server in servers {
                let serverName = try XCTUnwrap(server["name"] as? String)
                let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
                let tools = try entries.map { try XCTUnwrap($0["tool"] as? [String: Any]) }
                let catalog = try JSONDecoder().decode(
                    DecodedJSONValue.self, from: JSONSerialization.data(withJSONObject: ["tools": tools])
                )
                let bridge = try NativeChatMCPToolBridge(catalog: catalog)
                XCTAssertEqual(bridge.tools.count, tools.count, serverName)
                descriptors.append(contentsOf: bridge.tools.map {
                    AuthorizedDescriptor(
                        server: serverName,
                        name: $0.name,
                        description: $0.description,
                        parameters: $0.parameters
                    )
                })
            }
            return descriptors
        }

        private static func jsonInt(_ value: Any?) -> Int? {
            (value as? NSNumber)?.intValue
        }

        private static func nativeRevision() -> String? {
            var url = Bundle(for: NativeChatFoundationModelsPinnedSDKAdmissionTests.self).bundleURL
            for _ in 0 ..< 16 {
                if FileManager.default.fileExists(atPath: url.appendingPathComponent(".git").path) {
                    return command("/usr/bin/git", ["-C", url.path, "rev-parse", "HEAD"])
                }
                let parent = url.deletingLastPathComponent()
                if parent.path == url.path { break }
                url = parent
            }
            let fallback = ProcessInfo.processInfo.environment["GITHUB_SHA"]
            guard let fallback, fallback.count == 40 else { return nil }
            return fallback
        }

        private static func producerManifestSHA(root: URL) -> String? {
            guard let data = try? Data(contentsOf: root.appendingPathComponent("manifest.json")),
                  let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let filaments = json["filaments"] as? [String: Any] else { return nil }
            return filaments["sha"] as? String
        }

        private static func command(_ path: String, _ arguments: [String]) -> String? {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: path)
            process.arguments = arguments
            let pipe = Pipe()
            process.standardOutput = pipe
            process.standardError = FileHandle.nullDevice
            do { try process.run() } catch { return nil }
            process.waitUntilExit()
            guard process.terminationStatus == 0 else { return nil }
            let text = String(data: pipe.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8)?
                .trimmingCharacters(in: .whitespacesAndNewlines)
            guard let text, !text.isEmpty else { return nil }
            return text
        }

        private static func scrub(_ text: String) -> String {
            let kept = text.split(separator: "\n", omittingEmptySubsequences: false).filter { line in
                let upper = line.uppercased()
                return !upper.contains("_TOKEN") && !upper.contains("TOKEN=") && !upper.contains("SECRET") &&
                    !upper.contains("PASSWORD") && !upper.contains("CREDENTIAL") && !upper.contains("API_KEY") &&
                    !line.contains("Environment:")
            }.joined(separator: "\n")
            return String(kept.suffix(1_500))
        }
    }

    private struct AuthorizedDescriptor {
        let server: String
        let name: String
        let description: String
        let parameters: Data
    }

    private struct ProbeJournal {
        let url: URL
        func append(_ fields: [String: Any]) throws {
            if !FileManager.default.fileExists(atPath: url.path) {
                FileManager.default.createFile(atPath: url.path, contents: Data())
            }
            let data = try JSONSerialization.data(withJSONObject: fields, options: [.sortedKeys])
            let handle = try FileHandle(forWritingTo: url)
            defer { try? handle.close() }
            _ = try handle.seekToEnd()
            try handle.write(contentsOf: data)
            try handle.write(contentsOf: Data([0x0A]))
            try handle.synchronize()
        }

        func lines() throws -> [[String: Any]] {
            guard FileManager.default.fileExists(atPath: url.path) else { return [] }
            return try String(contentsOf: url, encoding: .utf8).split(separator: "\n").compactMap { line in
                (try? JSONSerialization.jsonObject(with: Data(line.utf8))) as? [String: Any]
            }
        }

        func latestByName() throws -> [String: [String: Any]] {
            var latest: [String: [String: Any]] = [:]
            for line in try lines() {
                if let name = line["name"] as? String { latest[name] = line }
            }
            return latest
        }

        func latestAdmission() throws -> [String: Any]? {
            let parsed = try lines()
            return parsed.last { $0["phase"] as? String == "admission" && $0["status"] as? String != "counting" } ??
                parsed.last { $0["phase"] as? String == "admission" }
        }
    }

    private typealias ProbeChild = (exitCode: Int32, signal: Int32?, timedOut: Bool, tail: String)

    private enum ProbeError: Error {
        case boundaryMismatch, headroomWindowMissed, overflowUnreachable, missingEnvironment, bundle(String)
    }
#endif
