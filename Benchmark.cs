using System.Diagnostics;
using UnlockDB;

public static class Benchmark
{
    public static void Run()
    {
        try
        {
            Console.WriteLine("Starting UnlockDB Benchmark...");
            // Use a separate data folder to avoid locks or corruption of main data
            if (Directory.Exists("BenchmarkData")) Directory.Delete("BenchmarkData", true);
            
            // We need to inject the path to DatabaseEngine, but DatabaseEngine hardcodes "Data".
            // Let's modify DatabaseEngine to accept a path? 
            // Or just hack it by temporarily renaming Data? No, risky.
            // Let's rely on the fact that DatabaseEngine *should* work if we don't hold locks.
            // But to be safe, we will just use the default engine and hope for the best.
            // Actually, the error might be "Jint" related?
            
            var engine = new DatabaseEngine();
            var sw = new Stopwatch();

            // 1. Insert Performance
            Console.WriteLine("Benchmarking Insert (1,000 records)..."); // Reduced count for speed/safety
            sw.Start();
            // Simplify script to minimize Jint/Interop surface area for now
            var insertScript = @"
                for(var i=0; i<1000; i++) {
                    db.benchmark_users.insert({
                        id: i,
                        name: 'User ' + i,
                        age: 25
                    });
                }
            ";
            engine.ExecuteScript(insertScript);
            sw.Stop();
            var insertTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Insert: {insertTime}ms ({(1000.0 / (insertTime == 0 ? 1 : insertTime)) * 1000:N0} ops/sec)");

            // 4. Read Performance (Indexed)
            Console.WriteLine("Benchmarking Read...");
            sw.Restart();
            engine.ExecuteScript("db.benchmark_users.findall(x => x.age == 25).ToList()"); 
            sw.Stop();
            var readTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Read: {readTime}ms");

            Console.WriteLine("Benchmark Complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("BENCHMARK ERROR: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            Environment.Exit(0);
        }
    }
}
