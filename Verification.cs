using System;
using System.Collections.Generic;
using AxarDB;

public static class Verification
{
    public static void Run()
    {
        Console.WriteLine("Starting Verification Tests for Fixes...");
        var engine = new DatabaseEngine();

        // Setup Data
        var signals = engine.GetCollection("signals_test");
        // Clear previous data if any
        signals.Delete(x => true);

        signals.Insert(new Dictionary<string, object> { { "pair", "EURUSD" }, { "isPremium", true }, { "desc", "Premium Signal" } });
        signals.Insert(new Dictionary<string, object> { { "pair", "USDJPY" }, { "isPremium", false }, { "desc", "Free Signal" } });
        signals.Insert(new Dictionary<string, object> { { "pair", "GBPUSD" }, { "isPremium", true }, { "desc", "Another Premium" } });

        // Test 1: Boolean Filtering (isPremium == true)
        Console.WriteLine("\nTest 1: Boolean Filtering (isPremium == true)");
        // Test 1: Boolean Filtering (isPremium == true)
        Console.WriteLine("\nTest 1: Boolean Filtering (isPremium == true)");
        try
        {
            var script = "return db.signals_test.findall(x => x.isPremium == true).count();";
            var result = engine.ExecuteScript(script);
            Console.WriteLine($"Result: {result}");
            
            if (result.ToString() == "2")
                Console.WriteLine("PASS: Found 2 premium signals.");
            else
                Console.WriteLine($"FAIL: Expected 2, got {result}");
        }
                Console.WriteLine("PASS: Found 2 premium signals.");
            else
                Console.WriteLine($"FAIL: Expected 2, got {result}");
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: {ex}"); }

        // Test 2: camelCase .toList()
        Console.WriteLine("\nTest 2: camelCase .toList()");
        try
        {
            var script = "return db.signals_test.findall(x => x.isPremium == false).toList().Count;"; // Count property of List (C# property is Count, JS array length? Wrapper returns List<Object>, Jint treats as Array-like or HostObject. .Count works on List)
            // Actually Jint maps List.Count to .Count usually if CLR allowed.
            // Let's rely on .Count which is C# property.
            var result = engine.ExecuteScript(script);
            Console.WriteLine($"Result: {result}");
             if (result.ToString() == "1")
                Console.WriteLine("PASS: toList() worked.");
            else
                Console.WriteLine($"FAIL: Expected 1, got {result}");
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: {ex}"); }

        // Test 3: select().ToList()
        Console.WriteLine("\nTest 3: select().ToList()");
        try
        {
            var script = "return db.signals_test.findall().select(x => x.pair).ToList().Count;";
            var result = engine.ExecuteScript(script);
             if (result.ToString() == "3")
                Console.WriteLine("PASS: select().ToList() worked.");
            else
                Console.WriteLine($"FAIL: Expected 3, got {result}");
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: {ex}"); }
        
        // Test 4: select().toList() (camelCase on AxarList)
        Console.WriteLine("\nTest 4: select().toList()");
        try
        {
             var script = "return db.signals_test.findall().select(x => x.pair).toList().Count;";
             var result = engine.ExecuteScript(script);
             if (result.ToString() == "3")
                Console.WriteLine("PASS: select().toList() worked.");
            else
                Console.WriteLine($"FAIL: Expected 3, got {result}");
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: {ex}"); }

        Console.WriteLine("\nVerification Complete.");
        Environment.Exit(0);
    }
}
