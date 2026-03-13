#!/bin/zsh
set -e

SSH_KEY=~/.ssh/oracle_vm
SERVER=ubuntu@129.159.13.201
DEPLOY_DIR=/opt/vkine
PUBLISH_DIR=/tmp/vkine-publish

echo "==> Publishing..."
dotnet publish vkine.csproj -c Release -r linux-x64 --no-self-contained -o "$PUBLISH_DIR"

echo "==> Uploading..."
rsync -az --delete \
  --exclude='vkine.Tests*' \
  --exclude='coverlet*' \
  --exclude='Microsoft.CodeCoverage*' \
  --exclude='CodeCoverage' \
  --exclude='AWSSDK*' \
  --exclude='Castle.Core*' \
  "$PUBLISH_DIR/" "$SERVER:$DEPLOY_DIR/" \
  -e "ssh -i $SSH_KEY"

echo "==> Restarting service..."
ssh -i "$SSH_KEY" "$SERVER" "sudo systemctl restart vkine && sudo systemctl status vkine --no-pager"

echo "==> Done. https://129.159.13.201"
