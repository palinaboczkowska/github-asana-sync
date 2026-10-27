#!/usr/bin/env bash
# Registers the Asana webhook that drives the Asana -> GitHub direction.
# Run once, after the code with ReceiveAsanaWebhook is deployed.
#
# Asana calls the endpoint straight away with a handshake, which the Function
# answers by echoing the secret and storing it. The target URL carries the
# Function key, so nobody without it can perform that handshake. The Asana
# token is read from Key Vault and passed to curl through a temporary header
# file, never on the command line.
set -euo pipefail

RG=rg-github-asana-sync
ASANA_PROJECT_GID=${ASANA_PROJECT_GID:-1219583927991161}

SUB=$(az account show --query id -o tsv)
FUNC=$(basename "$(az resource list -g "$RG" --resource-type Microsoft.Web/sites --query "[0].id" -o tsv)")
KV=$(basename "$(az resource list -g "$RG" --resource-type Microsoft.KeyVault/vaults --query "[0].id" -o tsv)")
HOST="$FUNC.azurewebsites.net"

FUNCTION_KEY=$(az rest --method post \
  --url "https://management.azure.com/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Web/sites/$FUNC/host/default/listkeys?api-version=2022-03-01" \
  --query functionKeys.default -o tsv)

HEADERS=$(mktemp)
trap 'rm -f "$HEADERS"' EXIT
chmod 600 "$HEADERS"
printf 'Authorization: Bearer %s\n' "$(az keyvault secret show --vault-name "$KV" -n AsanaAccessToken --query value -o tsv)" > "$HEADERS"

# Wake the Function App first: a cold start can take longer than Asana is
# willing to wait for the handshake.
curl -s -o /dev/null -X POST "https://$HOST/api/webhooks/github" -d '{}' || true
sleep 5

curl -sS --fail-with-body -X POST "https://app.asana.com/api/1.0/webhooks" \
  -H @"$HEADERS" -H "Content-Type: application/json" \
  -d "{\"data\":{\"resource\":\"$ASANA_PROJECT_GID\",\"target\":\"https://$HOST/api/webhooks/asana?code=$FUNCTION_KEY\",\"filters\":[{\"resource_type\":\"task\",\"action\":\"changed\",\"fields\":[\"completed\"]}]}}" \
  | python3 -c 'import json,sys; d=json.load(sys.stdin)["data"]; print("Webhook registered:", d["gid"], "active:", d.get("active"))'
