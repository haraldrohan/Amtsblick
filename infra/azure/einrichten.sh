#!/usr/bin/env bash
# Einmalige Einrichtung in Azure, damit der Release-Workflow deployen darf.
# Legt an: eine Ressourcengruppe und eine Identität, der GitHub Actions über OIDC vertraut
# (kein Kennwort, kein gespeichertes Geheimnis). Den Container selbst legt erst der Workflow an.
#
#   az login
#   bash infra/azure/einrichten.sh <kontaktadresse> [domain]
#
# Kosten entstehen durch dieses Skript noch keine; sie beginnen mit dem ersten Deployment.
set -euo pipefail

kontakt="${1:?Kontaktadresse angeben}"
domain="${2:-}"
repo="haraldrohan/Amtsblick"
gruppe="amtsblick"
ort="austriaeast"
umgebung="produktion"

abo="$(az account show --query id -o tsv)"
tenant="$(az account show --query tenantId -o tsv)"

az group create --name "$gruppe" --location "$ort" --output none
az identity create --name amtsblick-github --resource-group "$gruppe" --location "$ort" --output none
client="$(az identity show --name amtsblick-github --resource-group "$gruppe" --query clientId -o tsv)"
principal="$(az identity show --name amtsblick-github --resource-group "$gruppe" --query principalId -o tsv)"

# Nur Läufe der GitHub-Umgebung "produktion" dieses Repositorys dürfen sich anmelden. GitHub weist
# sich mit unveränderlichen Kennungen aus (Besitzer@ID/Repository@ID); die ältere Form mit bloßen
# Namen wird zusätzlich hinterlegt.
besitzer_id="$(gh api "repos/$repo" -q .owner.id)"
repo_id="$(gh api "repos/$repo" -q .id)"
az identity federated-credential create --name github-produktion --identity-name amtsblick-github \
  --resource-group "$gruppe" --issuer https://token.actions.githubusercontent.com \
  --subject "repo:$repo:environment:$umgebung" --audiences api://AzureADTokenExchange --output none
az identity federated-credential create --name github-produktion-ids --identity-name amtsblick-github \
  --resource-group "$gruppe" --issuer https://token.actions.githubusercontent.com \
  --subject "repo:${repo%%/*}@$besitzer_id/${repo##*/}@$repo_id:environment:$umgebung" \
  --audiences api://AzureADTokenExchange --output none

# Rechte nur auf die eigene Ressourcengruppe.
az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "/subscriptions/$abo/resourceGroups/$gruppe" --output none

gh variable set AZURE_CLIENT_ID --repo "$repo" --body "$client"
gh variable set AZURE_TENANT_ID --repo "$repo" --body "$tenant"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$repo" --body "$abo"
gh variable set AZURE_RESSOURCENGRUPPE --repo "$repo" --body "$gruppe"
gh variable set AMTSBLICK_DOMAIN --repo "$repo" --body "$domain"
gh secret set AMTSBLICK_KONTAKT --repo "$repo" --body "$kontakt"

echo "Eingerichtet: Ressourcengruppe $gruppe in $ort, Identität amtsblick-github, Variablen im Repository $repo."
echo "Noch von Hand: in GitHub unter Settings > Environments die Umgebung \"$umgebung\" anlegen."
