set -e

dotnet publish --configuration Release
(cd bin/Release/net7.0/publish && rm -f foo.zip && zip -r foo.zip ./)
az webapp deployment source config-zip --resource-group search-indexer-testing --name 'datalake-indexer-func-dev' --src ./bin/Release/net7.0/publish/foo.zip