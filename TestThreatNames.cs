using System;
using System.IO;
using System.Threading.Tasks;
using Xdows.ScanEngine;

namespace TestThreatNames
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing threat name extraction functionality...");
            
            // Test with a sample file path (this would need to be replaced with an actual file)
            string testFilePath = "test.exe";
            
            // Test with ExtraData disabled (should return score-based result)
            string resultWithoutExtraData = await ScanEngine.LocalScanAsync(testFilePath, false, false);
            Console.WriteLine($"Result without ExtraData: {resultWithoutExtraData}");
            
            // Test with ExtraData enabled (should return threat name if available)
            string resultWithExtraData = await ScanEngine.LocalScanAsync(testFilePath, false, true);
            Console.WriteLine($"Result with ExtraData: {resultWithExtraData}");
            
            Console.WriteLine("Test completed.");
        }
    }
}