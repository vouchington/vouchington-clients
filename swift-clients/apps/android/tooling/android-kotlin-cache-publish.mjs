import { spawnSync } from 'node:child_process'

// Gradle uses a POSIX record lock. Keep its inode across the directory exchange so
// already-open Gradle clients and newly opened clients still share the same lock.
const publisher = String.raw`
import ctypes, fcntl, json, os, stat, sys, time
p = json.load(sys.stdin)
root, staged = p['root'], p['staged']

def identity(path):
    s = os.lstat(path)
    if not stat.S_ISDIR(s.st_mode): raise RuntimeError('Unsafe Kotlin cache publication directory')
    return [str(s.st_dev), str(s.st_ino)]

def ancestors(path):
    while True:
        identity(path)
        parent = os.path.dirname(path)
        if parent == path: break
        path = parent

def exchange(a, b):
    libc = ctypes.CDLL(None, use_errno=True)
    if sys.platform == 'darwin':
        fn = libc.renamex_np
        fn.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint]
        result = fn(os.fsencode(a), os.fsencode(b), 2) # RENAME_SWAP
    elif sys.platform.startswith('linux'):
        fn = libc.renameat2
        fn.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
        result = fn(-100, os.fsencode(a), -100, os.fsencode(b), 2) # RENAME_EXCHANGE
    else: raise RuntimeError('Atomic Kotlin cache publication requires Darwin or Linux')
    if result: raise OSError(ctypes.get_errno(), 'Atomic Kotlin cache exchange failed')

ancestors(root)
ancestors(staged)
if identity(root) != p['identity']: raise RuntimeError('Kotlin cache destination changed')
mutex = root + '.restore-lock'
os.mkdir(mutex, 0o700)
mutex_identity = identity(mutex)
fd = None
created = False
published = False
try:
    if identity(root) != p['identity']: raise RuntimeError('Kotlin cache destination changed')
    lock = os.path.join(root, 'gradle-build-cache.lock')
    flags = os.O_RDWR | os.O_NOFOLLOW | os.O_NONBLOCK
    try: fd = os.open(lock, flags)
    except FileNotFoundError:
        fd = os.open(lock, flags | os.O_CREAT | os.O_EXCL, 0o600)
        created = True
    if not stat.S_ISREG(os.fstat(fd).st_mode): raise RuntimeError('Unsafe Gradle cache lock')
    fcntl.lockf(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    staged_lock = os.path.join(staged, 'gradle-build-cache.lock')
    os.link(lock, staged_lock, follow_symlinks=False)
    held = os.fstat(fd)
    for path in [lock, staged_lock]:
        linked = os.lstat(path)
        if not stat.S_ISREG(linked.st_mode) or (linked.st_dev, linked.st_ino) != (held.st_dev, held.st_ino):
            raise RuntimeError('Gradle cache lock changed during publication')
    ancestors(root)
    ancestors(staged)
    if identity(root) != p['identity']: raise RuntimeError('Kotlin cache destination changed')
    if time.time() * 1000 > p['deadline']: raise RuntimeError('Kotlin cache time limit exceeded')
    staged_identity = identity(staged)
    exchange(staged, root)
    if identity(staged) != p['identity']:
        if identity(root) == staged_identity: exchange(staged, root)
        raise RuntimeError('Kotlin cache destination changed during publication')
    published = True
finally:
    if created and not published and fd is not None:
        try:
            current = os.lstat(lock)
            own = os.fstat(fd)
            if (current.st_dev, current.st_ino) == (own.st_dev, own.st_ino): os.unlink(lock)
        except FileNotFoundError: pass
    if fd is not None: os.close(fd)
    if identity(mutex) == mutex_identity: os.rmdir(mutex)
`

export function publishKotlinCache(staged, root, identity, deadline) {
  const remaining = deadline - Date.now()
  if (remaining <= 0) throw Error('Kotlin cache time limit exceeded')
  const result = spawnSync('python3', ['-I', '-c', publisher], {
    input: JSON.stringify({ staged, root, identity, deadline }),
    encoding: 'utf8', timeout: remaining, maxBuffer: 8192,
  })
  if (result.error || result.status !== 0) {
    throw Error(`Kotlin cache publication failed: ${result.error?.message ?? result.stderr.trim()}`)
  }
}
