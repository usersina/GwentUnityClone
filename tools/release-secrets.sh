#!/usr/bin/env bash
# One-time setup of the secrets the Release workflow (.github/workflows/release.yml) needs, as GitHub Actions secrets
# of the repository: the Unity license it builds with, and the Android release key. Run it yourself:
#   tools/release-secrets.sh [owner/repo]
# Needs gh (signed in, with access to the repository), keytool (a JDK) and base64.
#
# - UNITY_LICENSE: the license file Unity Hub keeps once a Personal license is activated on this machine.
# - UNITY_EMAIL, UNITY_PASSWORD: your Unity account; gh asks for each (the password is not shown).
# - The Android release key is made once, here, and kept in ~/.config/gwentlegacy/ (only you can read it): back up
#   that folder. A later key would sign APKs that no longer install over the ones already out.
set -euo pipefail

repo=${1:-usersina/GwentUnityClone}
license="$HOME/.local/share/unity3d/Unity/Unity_lic.ulf"
keys="$HOME/.config/gwentlegacy"
keystore="$keys/gwentlegacy.keystore"
alias_name=gwentlegacy

for tool in gh keytool base64; do
  command -v "$tool" >/dev/null || { echo "Missing: $tool" >&2; exit 1; }
done
[ -f "$license" ] || { echo "No Unity license at $license: activate a Personal license in Unity Hub first." >&2; exit 1; }

echo "Unity license -> UNITY_LICENSE"
gh secret set UNITY_LICENSE -R "$repo" < "$license"
echo "Your Unity account email:"
gh secret set UNITY_EMAIL -R "$repo"
echo "Your Unity account password:"
gh secret set UNITY_PASSWORD -R "$repo"

if [ ! -f "$keystore" ]; then
  echo "Making the Android release key in $keys"
  mkdir -p "$keys"
  chmod 700 "$keys"
  umask 077
  od -An -N24 -tx1 /dev/urandom | tr -d ' \n' > "$keystore.password"   # 48 random hex digits
  GWENT_KEY_PASSWORD=$(cat "$keystore.password") keytool -genkeypair -keystore "$keystore" -alias "$alias_name" \
    -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Gwent Legacy" \
    -storepass:env GWENT_KEY_PASSWORD -keypass:env GWENT_KEY_PASSWORD
fi

echo "Android release key -> ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS, ANDROID_KEY_ALIAS, ANDROID_KEY_PASS"
base64 -w0 "$keystore" | gh secret set ANDROID_KEYSTORE_BASE64 -R "$repo"
gh secret set ANDROID_KEYSTORE_PASS -R "$repo" < "$keystore.password"
printf '%s' "$alias_name" | gh secret set ANDROID_KEY_ALIAS -R "$repo"
gh secret set ANDROID_KEY_PASS -R "$repo" < "$keystore.password"

echo
echo "Done. Back up $keys: it is the only copy of the Android release key."
echo "Local builds sign with it too when these are set:"
echo "  export GWENT_KEYSTORE=$keystore GWENT_KEY_ALIAS=$alias_name"
echo "  export GWENT_KEYSTORE_PASS=\$(cat $keystore.password) GWENT_KEY_PASS=\$(cat $keystore.password)"
