const properties = {
  'sys.boot_completed': '1',
  'ro.build.version.sdk': '35',
  'ro.build.fingerprint': 'google/sdk_gphone64_x86_64/test-build',
  'ro.product.cpu.abilist': 'x86_64,arm64-v8a',
  'ro.dalvik.vm.native.bridge': 'libndk_translation.so',
}

const instrumentationRunner =
  'instrumentation:ai.voucha.android.test/androidx.test.runner.AndroidJUnitRunner (target=ai.voucha.android)'

// Answers the adb calls a passing API 35 session makes. Unknown getprop names
// return an empty string, matching an adb property that is unset.
export function passingAdbCommand(_program, args) {
  if (args[0] === 'shell' && args[1] === 'getprop') return properties[args[2]] ?? ''
  if (args[0] === 'shell' && args[1] === 'pm' && args[2] === 'list') return instrumentationRunner
  if (args[0] === 'shell' && args[1] === 'am' && args[2] === 'instrument') {
    const [className, methodName] = args[7].split('voucha.android.')[1].split('#')
    return [
      `INSTRUMENTATION_STATUS: class=voucha.android.${className}`,
      `INSTRUMENTATION_STATUS: test=${methodName}`,
      'INSTRUMENTATION_STATUS_CODE: 0',
      'OK (1 test)',
      'INSTRUMENTATION_CODE: -1',
    ].join('\n')
  }
  return ''
}
