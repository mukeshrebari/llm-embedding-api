#!/usr/bin/env bash
# Computes the SHA-256 hex hash of an API key, for use as Auth:ApiKeys:*:KeyHash.
#
# The plaintext key itself is never stored in configuration — only this hash. Generate
# a key with a password manager or `openssl rand`, hash it here, and put the HASH in
# config/Key Vault/env vars. Distribute the plaintext key to the consuming service out
# of band (it cannot be recovered from the hash).
#
# Usage:
#   ./hash-key.sh                 # prompts for the key (not echoed)
#   ./hash-key.sh 'my-plaintext-key'   # pass the key as an argument (visible in shell history!)
#   echo -n 'my-plaintext-key' | ./hash-key.sh --stdin
set -euo pipefail

hash_it() {
  # -n / printf avoids a trailing newline from being hashed
  if command -v sha256sum >/dev/null 2>&1; then
    printf '%s' "$1" | sha256sum | awk '{print $1}'
  else
    # macOS/BSD fallback
    printf '%s' "$1" | shasum -a 256 | awk '{print $1}'
  fi
}

if [[ "${1:-}" == "--stdin" ]]; then
  key="$(cat -)"
  hash_it "$key"
elif [[ -n "${1:-}" ]]; then
  hash_it "$1"
else
  read -r -s -p "API key to hash: " key
  echo >&2
  hash_it "$key"
fi
