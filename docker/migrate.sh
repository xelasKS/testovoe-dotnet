#!/bin/bash
set -e
export PATH="${PATH}:/root/.dotnet/tools"

echo ">> migrating Commissions"
dotnet ef database update --project ClientCommissions.Commissions --connection "$COMMISSIONS_CONNECTION"
echo ">> migrating Users"
dotnet ef database update --project ClientCommissions.Users --connection "$USERS_CONNECTION"
echo ">> migrating Wallet"
dotnet ef database update --project ClientCommissions.Wallet --connection "$WALLET_CONNECTION"
echo ">> all migrations applied"
