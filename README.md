# AzureDataLakeIndexer
Azure Datalake gen2 search indexer fiddling
The reason this exists, is because the built in datalake indexers in azure search are slow... mind numbingly slow. 
Most of the time the indexers seem to spend on listing paths in datalake and this project solves this by using a helper index for paths. Querying this index for modified files is much faster than listing paths in datalake
The built in indexers also has a habit of forgetting to renew the access tokens even when using RBAC causing indexing to fail.


## Overview

```mermaid
flowchart LR
    datalake[(DataLake)]

    datalake-->|BlobCreated|pathfunc
    datalake-->|BlobDeleted|pathfunc
    
    pathfunc-->|UpsertPaths|pathindex
    pathfunc-->|UpsertPaths|deletedpathindex

    subgraph FuncHost
      pathfunc{{PathFunc}}
      indexerfunc{{IndexerFunc}}
    end

    subgraph AzureSearch
      pathindex[(Path index)]
      deletedpathindex[(Deleted Path index)]
      dataindex[(Data index)]
    end

    pathindex-->|ListPaths|indexerfunc
    datalake-->|Readdocs|indexerfunc
    indexerfunc-->|UpsertData|dataindex  

```
