import uuid
import json

class AxarBaseModel:
    def __init__(self):
        self._id = str(uuid.uuid4())

    @property
    def id(self):
        return self._id

    @id.setter
    def id(self, value):
        self._id = value

    def to_dict(self):
        # Create a dictionary from the object's properties
        # We need to map 'id' to '_id' for the database
        # self.__dict__ contains _id, so we are good if we just use that?
        # But we also want other properties.
        # Assuming subclasses set properties as instance attributes.
        d = self.__dict__.copy()
        
        # If there are other private variables or methods, we might want to filter them.
        # For now, simple dump.
        return d
