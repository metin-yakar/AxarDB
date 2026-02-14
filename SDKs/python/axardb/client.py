import requests
import base64
import json
import logging
from urllib.parse import urlencode
from .ratelimiter import AxarRateLimiter
from .builder import AxarQueryBuilder
from .base_model import AxarBaseModel

class AxarClient:
    def __init__(self, base_url, username, password, logger=None):
        self._base_url = base_url.rstrip('/')
        self._session = requests.Session()
        
        auth_str = f"{username}:{password}"
        b64_auth = base64.b64encode(auth_str.encode()).decode()
        self._session.headers.update({
            "Authorization": f"Basic {b64_auth}",
            "Content-Type": "text/plain" # Default for script body
        })
        
        self._logger = logger or logging.getLogger("AxarDB")
        self._rate_limiter = AxarRateLimiter(self._logger)

    def configure_rate_limit(self, limit_type, max_requests):
        self._rate_limiter.set_limit(limit_type, max_requests)

    def collection(self, collection_name):
        return AxarQueryBuilder(self, collection_name)

    def query_with_rate_limit(self, script, parameters, limit_key, limit_duration, limit_type, limit_condition=None):
        if self._rate_limiter.check_limit(limit_key, limit_duration, limit_type, limit_condition):
            self._rate_limiter.log_restriction(limit_key, limit_duration, limit_type, limit_condition)
            raise Exception(f"Rate limit exceeded for {limit_type} on {limit_key}.")
            
        return self.query(script, parameters)

    def query(self, script, parameters=None):
        url = f"{self._base_url}/query"
        
        # Prepare parameters for query string
        params_dict = {}
        if parameters:
            for k, v in parameters.items():
                if isinstance(v, (dict, list)):
                    params_dict[k] = json.dumps(v)
                else:
                    params_dict[k] = str(v)
        
        response = self._session.post(url, data=script.encode('utf-8'), params=params_dict)
        
        if not response.ok:
            raise Exception(f"AxarDB Error ({response.status_code}): {response.text}")
            
        text = response.text
        if not text:
            return None
            
        try:
            return response.json()
        except ValueError:
            # Return raw text if not JSON? Or try to cast?
            # C# tries to cast. Python is dynamic, so returning text or int is fine.
            # If "100" comes back as "100", json() parses it as int.
            # If "hello" comes back, json() fails.
            return text

    def execute(self, script, parameters=None):
        self.query(script, parameters)

    def close(self):
        self._session.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()

    # Helper methods
    
    def insert(self, collection, document):
        doc = document
        if isinstance(document, AxarBaseModel):
            doc = document.to_dict()
        
        # Sanitize empty IDs to avoid overwriting on server
        if isinstance(doc, dict):
            # Check for empty string or nil GUID
            if '_id' in doc and (not doc['_id'] or doc['_id'] == "00000000-0000-0000-0000-000000000000"):
                del doc['_id']

        json_doc = json.dumps(doc)
        script = f"db.{collection}.insert({json_doc})"
        return self.query(script)

    def find_all(self, collection, predicate=None):
        pred_str = predicate if predicate else ""
        script = f"db.{collection}.findall({pred_str}).toList()"
        return self.query(script)

    def find(self, collection, predicate):
        script = f"db.{collection}.find({predicate})"
        return self.query(script)

    def update(self, collection, predicate, update_data):
        json_data = json.dumps(update_data)
        script = f"db.{collection}.update({predicate}, {json_data})"
        self.execute(script)

    def delete(self, collection, predicate):
        script = f"db.{collection}.findall({predicate}).delete()"
        self.execute(script)

    # Management Methods

    def create_view(self, name, script):
        self.execute("db.saveView(@name, @script)", {"name": name, "script": script})

    def call_view(self, name, parameters=None):
        url = f"{self._base_url}/views/{name}"
        
        # Prepare parameters for query string
        params_dict = {}
        if parameters:
            # Handle both dict and object
            iterator = parameters.items() if isinstance(parameters, dict) else parameters.__dict__.items()
            for k, v in iterator:
                if isinstance(v, (dict, list)):
                    params_dict[k] = json.dumps(v)
                else:
                    params_dict[k] = str(v)

        response = self._session.get(url, params=params_dict)
        text = response.text
        
        if not response.ok:
            error_msg = (f"Failed to call view '{name}'. Status: {response.status_code}.\n"
                         f"Expected Usage: GET /views/{{name}}?param1=value1\n"
                         f"Actual URL: {response.url}\n"
                         f"Server Response: {text}")
            
            if self._logger:
                self._logger.error(error_msg)
            
            raise Exception(f"AxarDB View Error: {error_msg}")
            
        if not text:
            return None
            
        try:
            return response.json()
        except ValueError:
            return text

    def create_trigger(self, name, collection, script):
        self.execute("db.saveTrigger(@name, @collection, @script)", 
                     {"name": name, "collection": collection, "script": script})

    def add_vault(self, key, value):
        self.execute("addVault(@key, @value)", {"key": key, "value": value})

    def create_index(self, collection, selector, descending=False):
        # selector is safe string execution
        script = f"db.{collection}.index({selector})"
        if descending:
            script = f"db.{collection}.index({selector}, 'DESC')"
        self.execute(script)

    def create_user(self, username, password):
        self.execute("db.sysusers.insert({ username: @username, password: sha256(@password) })", 
                     {"username": username, "password": password})

    def join(self, collection1, collection2, where_condition):
        script = f"db.join(db.{collection1}, db.{collection2}).where({where_condition}).toList()"
        return self.query(script)

    def show_collections(self):
        return self.query("db.getCollections()")

    async def show_collections_async(self):
        import asyncio
        return await asyncio.to_thread(self.show_collections)

    async def insert_async(self, collection, document):
        import asyncio
        return await asyncio.to_thread(self.insert, collection, document)

    async def random_string_async(self, length):
        import asyncio
        return await asyncio.to_thread(self.query, "random(@length)", {"length": length})

