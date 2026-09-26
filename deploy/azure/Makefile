# Azure Container Apps deploy.
#   make -C deploy/azure deploy    # aspire deploy (ACA + ACR + push + deploy), or azd up
#   make -C deploy/azure provision # azd provision only
.PHONY: deploy provision
deploy:
	@bash deploy.sh
provision:
	@azd provision
