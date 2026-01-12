using PeNet;
using Self_Heuristic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Xdows.ScanEngine
{
    public static class ScanEngine
    {
        public record PEInfo
        {
            public string[]? ImportsDll;
            public string[]? ImportsName;
            public string[]? ExportsName;
        }

        public static async Task<string> LocalScanAsync(string path, bool deep, bool ExtraData)
        {
            if (!File.Exists(path)) return string.Empty;

            if (!PeFile.IsPeFile(path))
            {
                try
                {
                    var fileContent = await File.ReadAllBytesAsync(path);
                    var scriptScanResult = await ScriptScan.ScanScriptFileAsync(path, fileContent);
                    if (scriptScanResult.score >= 75)
                    {
                        if (ExtraData)
                        {
                            // 当启用额外数据时，使用威胁名称而不是分数
                            if (!string.IsNullOrEmpty(scriptScanResult.threatName))
                            {
                                // 只保留威胁名称，不要额外的后缀信息
                                return scriptScanResult.threatName;
                            }
                            else
                            {
                                return $"code{scriptScanResult.score}";
                            }
                        }
                        else
                        {
                            return $"code{scriptScanResult.score}";
                        }
                    }
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            var peFile = new PeFile(path);
            var fileInfo = new PEInfo();

            if (peFile.IsDll)
            {
                var exports = peFile.ExportedFunctions;
                if (exports != null)
                {
                    fileInfo.ExportsName = [.. exports.Select(exported => exported.Name ?? string.Empty)];
                }
                else
                {
                    fileInfo.ExportsName = [];
                }
            }
            var importedFunctions = peFile.ImportedFunctions;
            if (importedFunctions != null)
            {
                var validImports = importedFunctions
                    .Where(import => import.Name != null)
                    .ToList();

                fileInfo.ImportsDll = [.. validImports.Select(import => import.DLL)];
                fileInfo.ImportsName = [.. validImports.Select(import => import.Name ?? string.Empty)];
            }
            else
            {
                fileInfo.ImportsDll = [];
                fileInfo.ImportsName = [];
            }

            var score = await Heuristic.Evaluate(path, peFile, fileInfo, deep);
            if (score.score >= 75)
            {
                if (ExtraData)
                {
                    // 当启用额外数据时，使用威胁名称而不是分数
                    if (!string.IsNullOrEmpty(score.threatName))
                    {
                        // 只保留威胁名称，不要额外的后缀信息
                        return score.threatName;
                    }
                    else
                    {
                        return $"code{score.score}";
                    }
                }
                else
                {
                    return $"code{score.score}";
                }
            }
            return string.Empty;
        }
        //public static string SignedAndValid = string.Empty;

        private static readonly System.Net.Http.HttpClient s_httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
        
        // 并发扫描相关字段
        private static readonly SemaphoreSlim s_scanSemaphore = new(5000, 5000);
        private static readonly Dictionary<string, Task<(int statusCode, string? result)>> s_pendingScans = new();
        private static readonly object s_pendingLock = new object();
        public static async Task<(int statusCode, string? result)> CzkCloudScanAsync(string path, string apiKey)
        {
            var client = s_httpClient;
            string hash = await GetFileMD5Async(path);
            string url = $"https://cv.szczk.top/scan/{apiKey}/{hash}";
            try
            {
                var resp = await client.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                string json = await resp.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("result", out JsonElement prop))
                    return (200, prop.GetString());
            }
            catch (HttpRequestException ex)
            {
                return ((int?)ex.StatusCode ?? -1, string.Empty);
            }

            return (-1, string.Empty);
        }
        public static async Task<(int statusCode, string? result)> CloudScanAsync(string path)
        {
            // 获取文件MD5作为唯一标识
            string hash = await GetFileMD5Async(path);
            
            // 检查是否已有相同的扫描在进行中
            Task<(int statusCode, string? result)>? existingTask;
            lock (s_pendingLock)
            {
                if (s_pendingScans.TryGetValue(hash, out existingTask))
                {
                    // 如果已有相同文件的扫描在进行中，直接返回该任务
                    // 注意：不能在lock内使用await，所以先获取任务再在lock外await
                    var taskToAwait = existingTask;
                }
            }
            // 在lock外await
            if (existingTask != null)
            {
                return await existingTask;
            }
            
            // 创建新的扫描任务
            var scanTask = PerformConcurrentCloudScanAsync(hash);
            
            // 将任务添加到待处理列表
            lock (s_pendingLock)
            {
                s_pendingScans[hash] = scanTask;
            }
            
            try
            {
                // 等待扫描完成
                var result = await scanTask;
                return result;
            }
            finally
            {
                // 从待处理列表中移除
                lock (s_pendingLock)
                {
                    s_pendingScans.Remove(hash);
                }
            }
        }
        
        // 实际执行并发云扫描的方法
        private static async Task<(int statusCode, string? result)> PerformConcurrentCloudScanAsync(string hash)
        {
            // 使用信号量控制并发数量
            await s_scanSemaphore.WaitAsync();
            
            try
            {
                string url = $"http://103.118.245.82:5000/scan/md5?key=my_virus_key_2024&md5={hash}";
                
                var resp = await s_httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                string json = await resp.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("scan_result", out JsonElement prop))
                    return (200, prop.GetString());
                
                return (-1, string.Empty);
            }
            catch (HttpRequestException ex)
            {
                return ((int?)ex.StatusCode ?? -1, string.Empty);
            }
            finally
            {
                s_scanSemaphore.Release();
            }
        }
        public static async Task<string> GetFileMD5Async(string path)
        {
            using var md5 = MD5.Create();
            await using var stream = File.OpenRead(path);
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }
        public class SouXiaoEngineScan
        {
            private Core? SouXiaoCore;
        
            private readonly Boolean IsDebug = false;

            public bool Initialize()
            {
                try
                {
                    SouXiaoCore = new(0.75, Directory.GetCurrentDirectory());
                    //SouRuleEngine = new();
                    //if (!SouRuleEngine.Initialization) { return false; }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public static (bool IsVirus, string Result) ScanFileByRuleEngine(string path)
            {
                //try
                //{
                //    if (SouRuleEngine == null)
                //    {
                //        throw new InvalidOperationException("SouXiaoRule is not initialized.");
                //    }
                //    bool scanResult = SouRuleEngine.ScanFile(path);
                //    if (scanResult)
                //    {
                //        return (true, "SouXiaoRule.Hit");
                //    }
                //    else
                //    {
                return (false, string.Empty);
                //}
                //}
                //catch (Exception)
                //{
                //    if (IsDebug) { throw; }

                //    return (false, string.Empty);
                //}
            }

            public (bool IsVirus, string Result) ScanFile(string path)
            {
                try
                {
                    if (SouXiaoCore == null)
                    {
                        throw new InvalidOperationException("SouXiaoCore is not initialized.");
                    }
                    bool scanResult = SouXiaoCore.Run(path);
                    if (scanResult)
                    {
                        return (true, "SouXiao.Hit");
                    }
                    else
                    {
                        return (false, string.Empty);
                    }
                }
                catch (Exception)
                {
                    if (IsDebug) { throw; }

                    return (false, string.Empty);
                }
            }
        }
        public static async Task<(int statusCode, string? result)> AXScanFileAsync(string targetFilePath)
        {
            if (!File.Exists(targetFilePath))
                throw new FileNotFoundException($"Target file not found: {targetFilePath}");

            string baseDir = AppContext.BaseDirectory;
            string axApiExePath = Path.Combine(baseDir, "AX_API", "AX_API.exe");

            if (!File.Exists(axApiExePath))
                throw new FileNotFoundException($"AX_API.exe not found at: {axApiExePath}");

            string escapedTargetPath = $"\"{targetFilePath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = axApiExePath,
                Arguments = $"-PE \"{escapedTargetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string output = await outputTask;
                string error = await errorTask;

                string result = !string.IsNullOrEmpty(output) ? output : error;
                using var doc = JsonDocument.Parse(result);

                if (doc.RootElement.TryGetProperty("status", out var statusProp) &&
                    statusProp.GetString() == "success")
                {
                    if (doc.RootElement.TryGetProperty("detected_threats", out var threatsArray) &&
                        threatsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var threat in threatsArray.EnumerateArray())
                        {
                            if (threat.TryGetProperty("type", out var typeProp))
                            {
                                return (200, typeProp.GetString() ?? string.Empty);
                            }
                        }

                        return (-1, string.Empty);
                    }
                }

                return (-1, string.Empty);
            }
            catch (Exception ex)
            {
                return (-1, $"Exception during scan: {ex.Message}");
            }
        }
    }
}