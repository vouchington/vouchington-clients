export function prefixedFailure(prefix) {
  return message => {
    throw new Error(`${prefix}: ${message}`)
  }
}
