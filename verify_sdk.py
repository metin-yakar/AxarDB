import sys
import os
import asyncio
import inspect

# Add SDK to path
sys.path.append(os.path.abspath("SDKs/python"))

try:
    from axardb.client import AxarClient
    from axardb.builder import AxarQueryBuilder
    print("Successfully imported AxarClient and AxarQueryBuilder")
except ImportError as e:
    print(f"Failed to import SDK: {e}")
    sys.exit(1)

async def main():
    client = AxarClient("http://localhost:5000", "admin", "admin")
    
    # Verify methods exist
    methods = [
        ("show_collections", client.show_collections),
        ("show_collections_async", client.show_collections_async),
        ("insert_async", client.insert_async),
        ("random_string_async", client.random_string_async)
    ]

    print("\nChecking Client methods:")
    for name, method in methods:
        if callable(method):
            print(f"  [OK] {name} exists and is callable")
            if asyncio.iscoroutinefunction(method):
                 print(f"       -> is async")
        else:
            print(f"  [FAIL] {name} missing or not callable")

    # Verify Builder methods
    builder = client.collection("test")
    print("\nChecking Builder methods:")
    if hasattr(builder, "select_async") and callable(builder.select_async):
        print("  [OK] select_async exists")
        if asyncio.iscoroutinefunction(builder.select_async):
            print("       -> is async")
    else:
        print("  [FAIL] select_async missing")

    # Verify random_string_async functionality
    if asyncio.iscoroutinefunction(client.random_string_async):
        rnd = await client.random_string_async(10)
        print(f"\nRandom String (10): {rnd}")
        if len(rnd) == 10:
            print("  [OK] random_string_async length is correct")
        else:
            print("  [FAIL] random_string_async length is incorrect")

if __name__ == "__main__":
    asyncio.run(main())
