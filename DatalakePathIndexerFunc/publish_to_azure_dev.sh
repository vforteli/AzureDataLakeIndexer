set -e

dotnet publish -c Release --self-contained --runtime linux-x64 -o publish/ 
(cd publish && rm -f foo.zip && zip -r foo.zip ./)
az webapp deployment source config-zip --resource-group search-indexer-testing --name 'datalake-indexer-func-dev' --src ./publish/foo.zip