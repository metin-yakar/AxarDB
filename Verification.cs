using System;
using System.Collections.Generic;
using UnlockDB;

public static class Verification
{
    public static void Run()
    {
        Console.WriteLine("Starting Verification Tests...");
        var engine = new DatabaseEngine();

        // Test 1: Successful Replacement
        Console.WriteLine("\nTest 1: Placeholder Replacement");
        try
        {
            var script = "return @name;";
            var parameters = new Dictionary<string, object> { { "name", "UnlockDB" } };
            var result = engine.ExecuteScript(script, parameters);
            
            if (result?.ToString() == "UnlockDB")
            {
                Console.WriteLine("PASS: 'UnlockDB' returned correctly.");
            }
            else
            {
                Console.WriteLine($"FAIL: Expected 'UnlockDB', got '{result}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: Exception: {ex.Message}");
        }

        // Test 2: Injection Prevention (Quoting)
        Console.WriteLine("\nTest 2: Injection Prevention (Quoting)");
        try
        {
            // Try to inject unsafe code: "foo'); eval('alert(1)
            var maliciousInput = "foo'); eval('alert(1)";
            var script = "return @val;";
            var parameters = new Dictionary<string, object> { { "val", maliciousInput } };
            var result = engine.ExecuteScript(script, parameters);

            // Expect the result to be the literal string, NOT executed code
            if (result?.ToString() == maliciousInput)
            {
                 Console.WriteLine("PASS: Malicious input treated as string literal.");
            }
            else
            {
                 Console.WriteLine($"FAIL: Expected literal string, got '{result}'");
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"FAIL: Exception: {ex.Message}");
        }

        // Test 3: Input Validation (Blacklist)
        Console.WriteLine("\nTest 3: Input Validation (Blacklist)");
        try
        {
            var script = "return @bad;";
            var parameters = new Dictionary<string, object> { { "bad", "eval(1+1)" } };
            engine.ExecuteScript(script, parameters);
            Console.WriteLine("FAIL: Should have thrown InvalidOperationException.");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"PASS: Caught expected exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: Caught unexpected exception: {ex.GetType().Name} - {ex.Message}");
        }

        Console.WriteLine("\nVerification Complete.");
        Environment.Exit(0);
    }
}
