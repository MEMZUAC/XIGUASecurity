using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace PluginSystem
{
    // 自定义程序集加载上下文，用于隔离插件程序集
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string _pluginPath;
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _pluginPath = pluginPath;
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // 尝试从依赖解析器加载程序集
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // 尝试从插件目录加载
            string pluginDir = Path.GetDirectoryName(_pluginPath) ?? "";
            assemblyPath = Path.Combine(pluginDir, $"{assemblyName.Name}.dll");
            if (File.Exists(assemblyPath))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // 回退到默认加载上下文
            return null;
        }
    }

    public class Config
    {
        public string Name { get; set; }
        public string NameSpase { get; set; }
        public string EntryAddress { get; set; }
        public string EntryFunction { get; set; }
        public string Description { get; set; }
    }
    public enum LoadState
    {
        None,
        NotFound,
        Borken
    }

    internal class Loader
    {
        public Config config;
        public LoadState state;
        public Type type;

        public Page GetGrid(Assembly asm)
        {
            try
            {
                Type t = asm.GetType(config.EntryAddress);

                object obj = Activator.CreateInstance(t);

                MethodInfo mi = t.GetMethod(config.EntryFunction);
                return (Page)mi.Invoke(obj, []);
            }
            catch
            {
                return new Page();
            }
        }

        public Assembly Load(string Name)
        {
            if (!File.Exists($".\\Plugin\\{Name}\\PluginFramework.dll")) state = LoadState.Borken;

            try
            {
                // 获取绝对路径
                string pluginPath = Path.GetFullPath($".\\Plugin\\{Name}\\PluginFramework.dll");
                
                // 使用自定义加载上下文加载插件
                var loadContext = new PluginLoadContext(pluginPath);
                Assembly asm = loadContext.LoadFromAssemblyPath(pluginPath);
                
                // 设置type字段以避免编译警告
                type = asm.GetType(config?.EntryAddress) ?? typeof(object);
                
                return asm;
            }
            catch (Exception ex)
            {
                state = LoadState.Borken;
                
                // 添加更详细的错误信息
                System.Diagnostics.Debug.WriteLine($"插件加载失败: {Name}");
                System.Diagnostics.Debug.WriteLine($"错误类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"错误消息: {ex.Message}");
                
                if (ex is FileLoadException flex)
                {
                    System.Diagnostics.Debug.WriteLine($"文件加载异常详情: {flex.FileName}");
                }
                
                throw;
            }
        }
        public void LoadConfig(string Name)
        {
            if (string.IsNullOrEmpty(Name)) state = LoadState.NotFound;

            if (!File.Exists($".\\Plugin\\{Name}")) state = LoadState.NotFound;
            try
            {
                string json = File.ReadAllText($".\\Plugin\\{Name}\\Information.json");
                config = JsonSerializer.Deserialize<Config>(json);
            }
            catch { }
        }
    }
}
