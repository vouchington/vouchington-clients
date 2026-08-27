#if canImport(os)
    import os
#endif

public struct VouchaLogger {
    #if canImport(os)
        private let logger: Logger

        public init(subsystem: String = "ai.voucha", category: String) {
            logger = Logger(subsystem: subsystem, category: category)
        }

        public func debug(_ message: @autoclosure () -> String) {
            let msg = message()
            logger.debug("\(msg, privacy: .public)")
        }

        public func info(_ message: @autoclosure () -> String) {
            let msg = message()
            logger.info("\(msg, privacy: .public)")
        }

        public func error(_ message: @autoclosure () -> String) {
            let msg = message()
            logger.error("\(msg, privacy: .public)")
        }
    #else
        public init(subsystem _: String = "ai.voucha", category _: String) {}

        public func debug(_ message: @autoclosure () -> String) {
            print("[DEBUG]", message())
        }

        public func info(_ message: @autoclosure () -> String) {
            print("[INFO]", message())
        }

        public func error(_ message: @autoclosure () -> String) {
            print("[ERROR]", message())
        }
    #endif
}
