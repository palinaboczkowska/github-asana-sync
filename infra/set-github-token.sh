#!/usr/bin/env bash
# Stores the GitHub token the Asana -> GitHub direction needs, by redeploying
# main.bicep with only that secret. The other secrets are left as they are
# in Key Vault, since empty parameters do not overwrite them.
#
# Create a fine-grained token first (GitHub > Settings > Developer settings >
# Fine-grained tokens): only this repository, permission "Issues: Read and
# write". Copy it, then run this script. The token is read from the
# clipboard and passed in a temporary parameters file, so it never appears on
# a command line or in the shell history.
set -euo pipefail

RG=rg-github-asana-sync
ASANA_PROJECT_GID=${ASANA_PROJECT_GID:-1219583927991161}

TOKEN=$(pbpaste | tr -d '[:space:]')
case "$TOKEN" in
  github_pat_*|ghp_*) ;;
  *) echo "The clipboard does not hold a GitHub token. Copy the token and run this again." >&2; exit 1 ;;
esac

STORAGE=$(basename "$(az resource list -g "$RG" --resource-type Microsoft.Storage/storageAccounts --query "[0].id" -o tsv)")
PACKAGE_URI="https://$STORAGE.blob.core.windows.net/deployments/app.zip"

PARAMS=$(mktemp)
trap 'rm -f "$PARAMS"' EXIT
chmod 600 "$PARAMS"
cat > "$PARAMS" <<JSON
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "githubToken": { "value": "$TOKEN" },
    "asanaProjectGid": { "value": "$ASANA_PROJECT_GID" },
    "packageUri": { "value": "$PACKAGE_URI" }
  }
}
JSON

az deployment group create -g "$RG" -f "$(dirname "$0")/main.bicep" -p "@$PARAMS" --query properties.provisioningState -o tsv
echo "GitHub token stored in Key Vault and wired into the Function App."
