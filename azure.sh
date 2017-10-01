#!/bin/bash
set -e

# Needs: brew install azure-cli
# Push to Azure Container Registry
# az group create --name kitchen-responsible-rg --location westeurope
# az acr create --name dipsbot --resource-group kitchen-responsible-rg --admin-enabled --sku Basic
az acr login --name dipsbot
# az acr list --resource-group kitchen-responsible-rg --query "[].{acrLoginServer:loginServer}" --output table
# -> dipsbot.azurecr.io
docker tag dipsbot dipsbot.azurecr.io/dipsbot

# Run container locally
# docker run -p 5000:80 dipsbot.azurecr.io/dipsbot
docker push dipsbot.azurecr.io/dipsbot

# Run in Azure Container Instances
# az acr show --name dipsbot --query loginServer
# az acr credential show --name dipsbot --query "passwords[0].value"
az container delete --name dipsbot --resource-group kitchen-responsible-rg --yes
az container create --name dipsbot --image dipsbot.azurecr.io/dipsbot --cpu 1 --memory 1 --registry-password $AZUREPW --ip-address public -g kitchen-responsible-rg
# az container show --name dipsbot --resource-group kitchen-responsible-rg --query state
az container show --name dipsbot --resource-group kitchen-responsible-rg --query ipAddress.ip
