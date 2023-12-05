import json
import os
import requests


service_name = "srchhrurrdurtklsrch"
index_name = "path-created-index"
api_version = "2023-11-01"
admin_key = os.environ.get('SEARCH_ADMIN_KEY')

url = f"https://{service_name}.search.windows.net/indexes/{index_name}/analyze?api-version={api_version}"


headers = {
    "Content-Type": "application/json",
    "api-key": admin_key
}

data = {
    "text": "partition_0%2fCustomer_2*",
    "analyzer": "keyword"
}


response = requests.post(url, json=data, headers=headers)

print(json.dumps(response.json(), indent=2))

# {
#   "text": "Text to analyze",
#   "analyzer": "analyzer_name"
# }


# {
#   "text": "Text to analyze",
#   "tokenizer": "tokenizer_name",
#   "tokenFilters": (optional) [ "token_filter_name" ],
#   "charFilters": (optional) [ "char_filter_name" ]
# }
