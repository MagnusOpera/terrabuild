# Keep hash shape and the project/target path while ignoring execution-platform
# differences. The unit tests exercise the inputs to the resolved hashes.
def hash:
  if type == "string" and test("^[A-F0-9]{64}$") then "<hash>" else . end;

def cachekey:
  if type == "string" then sub("/[A-F0-9]{64}$"; "/<hash>") else . end;

(.nodes[] | select(.fingerprint != null) | .fingerprint) |=
  (.operationHashes |= map(hash)
   | .dependencyTargetHashes |= map(.hash |= hash)
   | .targetHash |= hash
   | .cacheKey |= cachekey)
| (.nodes[] | select(.cache.key != null) | .cache.key) |= cachekey
| .batches[] |= (.targetHash |= hash | .operationHashes |= map(hash))
