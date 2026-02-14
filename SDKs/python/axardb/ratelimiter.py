import time
from threading import Lock
import logging

class AxarRateLimiter:
    """
    In-memory rate limiter for AxarDB SDK.
    """
    def __init__(self, logger=None):
        self._logger = logger
        self._limits = {}  # type: limit -> int
        self._counters = {} # cacheKey -> {count, expiry}
        self._lock = Lock()

    def set_limit(self, limit_type, max_requests):
        with self._lock:
            self._limits[limit_type] = max_requests

    def check_limit(self, key, duration_str, limit_type, condition=None):
        duration = self._parse_duration(duration_str)
        limit = self._get_limit_for_type(limit_type)
        
        cache_key = f"ratelimit:{limit_type}:{key}:{condition or 'default'}"
        now = time.time()

        with self._lock:
            if cache_key not in self._counters:
                self._counters[cache_key] = {'count': 0, 'expiry': now + duration}
            
            entry = self._counters[cache_key]
            
            # Reset if expired
            if now > entry['expiry']:
                entry['count'] = 0
                entry['expiry'] = now + duration
            
            entry['count'] += 1
            
            return entry['count'] > limit

    def log_restriction(self, key, duration, limit_type, condition):
        if self._logger:
            self._logger.warning(
                f"Rate limit exceeded for {limit_type} on {key}. Duration: {duration}, Condition: {condition}"
            )

    def _get_limit_for_type(self, limit_type):
        with self._lock:
            return self._limits.get(limit_type, 1000) # Default buffer

    def _parse_duration(self, duration_str):
        if not duration_str:
            return 0
        
        try:
            unit = duration_str[-1].lower()
            value = float(duration_str[:-1])
            
            if unit == 's': return value
            if unit == 'm': return value * 60
            if unit == 'h': return value * 3600
            if unit == 'd': return value * 86400
        except:
            pass
            
        return 60 # Default fallback in seconds
