using Microsoft.Win32;
using System;
using System.IO;
using System.Security;
using System.Management;

namespace XIGUASecurity.Utils
{
    /// <summary>
    /// Xdows-Security检测工具类
    /// </summary>
    public static class XdowsSecurityDetector
    {
        /// <summary>
        /// 检测Xdows-Security是否已安装
        /// </summary>
        /// <returns>如果已安装返回true，否则返回false</returns>
        public static bool IsInstalled()
        {
            try
            {
                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "开始检测Xdows-Security是否已安装");
                
                // 使用WMI查询已安装的程序
                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "使用WMI查询已安装的程序");
                if (CheckInstalledPrograms())
                {
                    LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "通过WMI查询检测到Xdows-Security已安装");
                    return true;
                }
                
                // 备用方法：检查常见安装路径
                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "检查常见安装路径");
                if (CheckInstallationPaths())
                {
                    LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "通过安装路径检测到Xdows-Security已安装");
                    return true;
                }
                
                // 备用方法：检查注册表
                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "检查注册表");
                if (CheckRegistry())
                {
                    LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "通过注册表检测到Xdows-Security已安装");
                    return true;
                }
                
                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", "未检测到Xdows-Security安装");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"访问被拒绝: {ex.Message}");
                return false;
            }
            catch (SecurityException ex)
            {
                LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"安全异常: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "XdowsSecurityDetector", $"检测Xdows-Security时发生错误: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 使用WMI查询已安装的程序
        /// </summary>
        private static bool CheckInstalledPrograms()
        {
            try
            {
                // 可能的应用程序名称
                string[] appNames = {
                    "Xdows-Security",
                    "Xdows Security",
                    "XdowsSecurity",
                    "西瓜Siri",
                    "西瓜 Siri"
                };

                // 查询32位和64位系统中的已安装程序
                string[] wmiQueries = {
                    "SELECT * FROM Win32_Product",
                    "SELECT * FROM Win32_InstalledWin32Program"
                };

                foreach (string query in wmiQueries)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(query))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                try
                                {
                                    string? name = obj["Name"]?.ToString();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        foreach (string appName in appNames)
                                        {
                                            if (name.Contains(appName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", $"通过WMI找到Xdows-Security: {name}");
                                                return true;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"处理WMI对象时发生错误: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"执行WMI查询时发生错误: {ex.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "XdowsSecurityDetector", $"使用WMI查询已安装程序时发生错误: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 检查常见安装路径
        /// </summary>
        private static bool CheckInstallationPaths()
        {
            try
            {
                // 常见安装路径
                string[] commonPaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                };

                // 可能的应用程序名称
                string[] appNames = {
                    "Xdows-Security",
                    "Xdows Security",
                    "XdowsSecurity",
                    "西瓜Siri",
                    "西瓜 Siri"
                };

                foreach (string basePath in commonPaths)
                {
                    foreach (string appName in appNames)
                    {
                        // 检查主程序文件
                        string[] exeFiles = {
                            Path.Combine(basePath, appName, "Xdows-Security.exe"),
                            Path.Combine(basePath, appName, "XdowsSecurity.exe"),
                            Path.Combine(basePath, appName, "Xdows Security.exe"),
                            Path.Combine(basePath, $"{appName}.exe")
                        };

                        foreach (string exeFile in exeFiles)
                        {
                            try
                            {
                                if (File.Exists(exeFile))
                                {
                                    LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", $"在路径找到Xdows-Security: {exeFile}");
                                    return true;
                                }
                            }
                            catch (IOException ex)
                            {
                                LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"检查文件时发生IO错误: {ex.Message}");
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "XdowsSecurityDetector", $"检查安装路径时发生错误: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 检查注册表
        /// </summary>
        private static bool CheckRegistry()
        {
            try
            {
                // 检查卸载信息
                string[] registryPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                
                // 可能的应用程序名称
                string[] appNames = {
                    "Xdows-Security",
                    "Xdows Security",
                    "XdowsSecurity",
                    "西瓜Siri",
                    "西瓜 Siri"
                };

                foreach (string registryPath in registryPaths)
                {
                    try
                    {
                        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryPath))
                        {
                            if (key != null)
                            {
                                foreach (string subKeyName in key.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (RegistryKey? subKey = key.OpenSubKey(subKeyName))
                                        {
                                            if (subKey != null)
                                            {
                                                object? displayName = subKey.GetValue("DisplayName");
                                                if (displayName != null)
                                                {
                                                    string appName = displayName.ToString() ?? string.Empty;
                                                    foreach (string name in appNames)
                                                    {
                                                        if (appName.Contains(name, StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            LogText.AddNewLog(LogLevel.INFO, "XdowsSecurityDetector", $"在注册表找到Xdows-Security: {appName}");
                                                            return true;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"检查注册表子项时发生错误: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (SecurityException ex)
                    {
                        LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"访问注册表时发生安全异常: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        LogText.AddNewLog(LogLevel.WARN, "XdowsSecurityDetector", $"访问注册表时被拒绝: {ex.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "XdowsSecurityDetector", $"检查注册表时发生错误: {ex.Message}");
                return false;
            }
        }
    }
}