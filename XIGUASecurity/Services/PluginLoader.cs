using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using XIGUASecurity.Plugins;

namespace XIGUASecurity.Services
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

    public class PluginLoader
    {
        public string PluginDirectory { get; }

        public PluginLoader(string? pluginDirectory = null)
        {
            PluginDirectory = pluginDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!Directory.Exists(PluginDirectory)) Directory.CreateDirectory(PluginDirectory);
        }

        public IEnumerable<IPlugin> LoadPlugins(object? host = null)
        {
            var list = new List<IPlugin>();
            try
            {
                // Discover built-in plugins from already loaded assemblies
                try
                {
                    var loaded = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var asm in loaded)
                    {
                        try
                        {
                            if (asm == null) continue;
                            
                            var types = asm.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                            foreach (var t in types)
                            {
                                try
                                {
                                    if (Activator.CreateInstance(t) is IPlugin plugin)
                                    {
                                        // 检查插件属性是否为null
                                        if (string.IsNullOrEmpty(plugin.Id) || 
                                            string.IsNullOrEmpty(plugin.Name) || 
                                            string.IsNullOrEmpty(plugin.Version))
                                        {
                                            continue; // 跳过属性不完整的插件
                                        }
                                        
                                        plugin.Initialize(host ?? Application.Current);
                                        list.Add(plugin);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // 记录插件初始化失败的详细信息
                                    System.Diagnostics.Debug.WriteLine($"Failed to initialize plugin {t?.Name}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录程序集加载失败的详细信息
                            System.Diagnostics.Debug.WriteLine($"Failed to load types from assembly {asm?.GetName().Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录发现内置插件失败的详细信息
                    System.Diagnostics.Debug.WriteLine($"Failed to discover built-in plugins: {ex.Message}");
                }

                // Discover external plugin DLLs in Plugins folder
                if (Directory.Exists(PluginDirectory))
                {
                    var dlls = Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
                    foreach (var dll in dlls)
                    {
                        try
                        {
                            // 使用自定义加载上下文加载插件
                            PluginLoadContext loadContext = new PluginLoadContext(dll);
                            var asm = loadContext.LoadFromAssemblyPath(dll);
                            
                            if (asm == null) 
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to load assembly from {dll}: Assembly is null");
                                continue;
                            }
                            
                            // 检查程序集是否兼容
                            if (!IsCompatibleAssembly(asm))
                            {
                                System.Diagnostics.Debug.WriteLine($"Assembly {dll} is not compatible with current runtime");
                                continue;
                            }
                            
                            var types = asm.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                            foreach (var t in types)
                            {
                                try
                                {
                                    if (Activator.CreateInstance(t) is IPlugin plugin)
                                    {
                                        // 检查插件属性是否为null
                                        if (string.IsNullOrEmpty(plugin.Id) || 
                                            string.IsNullOrEmpty(plugin.Name) || 
                                            string.IsNullOrEmpty(plugin.Version))
                                        {
                                            continue; // 跳过属性不完整的插件
                                        }
                                        
                                        plugin.Initialize(host ?? Application.Current);
                                        list.Add(plugin);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // 记录外部插件初始化失败的详细信息
                                    System.Diagnostics.Debug.WriteLine($"Failed to initialize external plugin {t?.Name} from {dll}: {ex.Message}");
                                }
                            }
                            
                            // 卸载加载上下文
                            loadContext.Unload();
                        }
                        catch (FileLoadException ex)
                        {
                            // 特别处理文件加载异常
                            System.Diagnostics.Debug.WriteLine($"FileLoadException loading {dll}: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Fusion log: {ex.InnerException?.Message ?? "No fusion log available"}");
                        }
                        catch (BadImageFormatException ex)
                        {
                            // 处理无效映像格式异常
                            System.Diagnostics.Debug.WriteLine($"BadImageFormatException loading {dll}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            // 记录程序集加载失败的详细信息
                            System.Diagnostics.Debug.WriteLine($"Failed to load assembly from {dll}: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().FullName}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录整个插件加载过程的异常
                System.Diagnostics.Debug.WriteLine($"Plugin loading failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            return list;
        }

        // 检查程序集是否与当前运行时兼容
        private bool IsCompatibleAssembly(Assembly assembly)
        {
            try
            {
                // 检查目标框架
                var targetFrameworkAttribute = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
                if (targetFrameworkAttribute != null)
                {
                    string frameworkName = targetFrameworkAttribute.FrameworkName;
                    // 简单检查是否包含.NET Core或.NET 5+
                    if (frameworkName.Contains(".NETCoreApp") || frameworkName.Contains(".NETFramework"))
                    {
                        return true;
                    }
                }
                
                // 如果没有目标框架属性，尝试检查类型
                assembly.GetTypes();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
