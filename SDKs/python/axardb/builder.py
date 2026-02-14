import json

class AxarQueryBuilder:
    def __init__(self, client, collection):
        self._client = client
        self._collection = collection
        self._where_clauses = []
        self._parameters = {}
        self._param_counter = 0
        self._take = None
        self._selector = None

    def where(self, property_or_expr, op=None, value=None, parameters=None):
        if op is None and value is None:
            # Raw expression mode: where("x.age > 18", params)
            if parameters:
                # In Python, we might pass a dict.
                # If parameters is an object, we copy its properties.
                if isinstance(parameters, dict):
                     self._parameters.update(parameters)
                elif hasattr(parameters, "__dict__"):
                     self._parameters.update(parameters.__dict__)
            
            self._where_clauses.append(property_or_expr)
        else:
            # Structured mode: where("age", ">", 18)
            param_name = f"p{self._param_counter}"
            self._param_counter += 1
            self._where_clauses.append(f"x.{property_or_expr} {op} @{param_name}")
            self._parameters[param_name] = value
            
        return self

    def take(self, count):
        self._take = count
        return self

    def select(self, selector):
        """
        Projects each element. selector is a string "x => x.name".
        """
        new_builder = AxarQueryBuilder(self._client, self._collection)
        new_builder._where_clauses = list(self._where_clauses)
        new_builder._parameters = self._parameters.copy()
        new_builder._param_counter = self._param_counter
        new_builder._take = self._take
        new_builder._selector = selector
        return new_builder

    def _build_base_script(self):
        script = f"db.{self._collection}"
        
        if self._where_clauses:
            predicate = " && ".join(self._where_clauses)
            script += f".findall(x => {predicate})"
        else:
            script += ".findall()"

        if self._take is not None:
            script += f".take({self._take})"

        if self._selector:
            script += f".select({self._selector})"

        return script

    def to_list(self):
        script = self._build_base_script() + ".toList()"
        return self._client.query(script, self._parameters)

    def first(self):
        script = self._build_base_script() + ".first()"
        return self._client.query(script, self._parameters)

    def count(self):
        script = self._build_base_script() + ".count()"
        return self._client.query(script, self._parameters)

    def update(self, update_data):
        param_name = f"update_{self._param_counter}"
        self._param_counter += 1
        self._parameters[param_name] = update_data
        
        script = self._build_base_script() + f".update(@{param_name})"
        self._client.execute(script, self._parameters)

    def delete(self):
        script = self._build_base_script() + ".delete()"
        self._client.execute(script, self._parameters)

    async def select_async(self, selector):
        import asyncio
        # We need to use the synchronous select() logic but execute the final query asynchronously
        # select() returns a NEW builder. It doesn't execute.
        # The user likely wants to execute a projection asynchronously.
        # "selectAsync projection metodu yok" -> Implies executing a projection.
        # In C# updates: SelectAsync<T>(selector) -> executes .Select(s).ToListAsync()
        # So here:
        builder = self.select(selector)
        return await asyncio.to_thread(builder.to_list)

