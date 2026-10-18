export {
  assertCheckoutMatchesSnapshot,
  assertPairSnapshotUnchanged,
  capturePairSnapshot,
  pairedRepositories,
  validateDispatch,
} from './paired-contract-policy.mjs'
export { createPairManifest } from './paired-contract-manifest.mjs'
export { validatePairManifest } from './paired-contract-manifest-validation.mjs'

import { runPairedContractCommand } from './paired-contract-commands.mjs'

export { runPairedContractCommand }

if (import.meta.main) await runPairedContractCommand(process.argv[2])
