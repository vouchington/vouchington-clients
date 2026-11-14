export function shouldSkipExporterIntegration({ required }) {
  return !required
}

export function assertRequiredExporterInputs({ required, dockerAvailable, bundleRoot }) {
  if (!required) return
  if (!dockerAvailable) throw new Error('the required isolated exporter test needs Docker')
  if (!bundleRoot) throw new Error('the required isolated exporter test needs the pinned bundle')
}
