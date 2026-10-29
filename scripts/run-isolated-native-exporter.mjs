import { fileURLToPath } from 'node:url'
import { runIsolatedNativeExporter } from './paired-exporter-runner.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'
export { runIsolatedNativeExporter } from './paired-exporter-runner.mjs'
export {
  assertUnprivilegedHostIdentity,
  decodeCandidateOutput,
} from './paired-exporter-output-protocol.mjs'

const fail = prefixedFailure('Isolated native exporter failed')

function parseArguments(argv) {
  if (
    argv.length !== 10 ||
    argv[0] !== '--producer-root' ||
    argv[2] !== '--consumer-root' ||
    argv[4] !== '--output-root' ||
    argv[6] !== '--package-bundle-root' ||
    argv[8] !== '--runtime-evidence-path'
  )
    fail(
      'usage: node scripts/run-isolated-native-exporter.mjs --producer-root <root> --consumer-root <root> --output-root <root> --package-bundle-root <root> --runtime-evidence-path <path>',
    )
  return {
    producerRoot: argv[1],
    consumerRoot: argv[3],
    outputRoot: argv[5],
    packageBundleRoot: argv[7],
    runtimeEvidencePath: argv[9],
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  await runIsolatedNativeExporter(parseArguments(process.argv.slice(2)))
