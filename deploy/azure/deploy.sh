#!/usr/bin/env bash
# Azure Container Apps deploy. The 2026-recommended path is the Aspire CLI; falls back to azd.
set -euo pipefail
cd "$(dirname "$0")/../.."                         # repo root
if command -v aspire >/dev/null 2>&1; then
  aspire deploy                                     # provision ACA + ACR + push + deploy
else
  echo "aspire CLI not found — using azd (azure.yaml)"
  azd up
fi
