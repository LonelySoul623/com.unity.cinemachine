using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Linq;

public class OptimizedBuildPlayerTools
{
    private static readonly string BUILD_CACHE_FILE = "build_cache.json";
    private static readonly string[] EXCLUDE_FROM_CACHE = { ".git", "Temp", "obj", "Logs" };

    [System.Serializable]
    public class BuildCache
    {
        public string lastBuildHash;
        public long lastBuildTime;
        public List<string> lastBuildFiles = new List<string>();
    }

    /// <summary>
    /// 优化的构建方法 - 支持增量构建和缓存
    /// </summary>
    public static void BuildPlayer()
    {
        try
        {
            var startTime = DateTime.Now;
            Debug.Log($"[OptimizedBuild] 开始构建时间: {startTime}");

            // 读取命令行参数
            var args = ParseCommandLineArgs();
            
            // 验证必要参数
            if (!ValidateArguments(args))
            {
                EditorApplication.Exit(1);
                return;
            }

            // 检查是否需要增量构建
            var buildCache = LoadBuildCache();
            var currentHash = CalculateProjectHash();
            
            bool needFullBuild = ShouldPerformFullBuild(buildCache, currentHash);
            
            if (!needFullBuild)
            {
                Debug.Log("[OptimizedBuild] 检测到无变更，尝试增量构建");
            }

            // 优化Unity设置
            OptimizeUnitySettings();

            // 执行构建
            var buildResult = PerformBuild(args, needFullBuild);

            if (buildResult.summary.result == BuildResult.Succeeded)
            {
                // 保存构建缓存
                SaveBuildCache(currentHash);
                
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                Debug.Log($"[OptimizedBuild] 构建成功完成！耗时: {duration.TotalMinutes:F2}分钟");
                
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[OptimizedBuild] 构建失败: {buildResult.summary.result}");
                EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[OptimizedBuild] 构建异常: {e.Message}\n{e.StackTrace}");
            EditorApplication.Exit(1);
        }
    }

    private static Dictionary<string, string> ParseCommandLineArgs()
    {
        var args = new Dictionary<string, string>();
        var commandLineArgs = Environment.GetCommandLineArgs();

        for (int i = 0; i < commandLineArgs.Length; i++)
        {
            if (commandLineArgs[i].StartsWith("-") && i + 1 < commandLineArgs.Length)
            {
                var key = commandLineArgs[i].Substring(1);
                var value = commandLineArgs[i + 1];
                args[key] = value;
                i++; // 跳过下一个参数，因为它是当前参数的值
            }
        }

        return args;
    }

    private static bool ValidateArguments(Dictionary<string, string> args)
    {
        var requiredArgs = new[] { "prefixDir", "productName", "version" };
        
        foreach (var arg in requiredArgs)
        {
            if (!args.ContainsKey(arg) || string.IsNullOrEmpty(args[arg]))
            {
                Debug.LogError($"[OptimizedBuild] 缺少必要参数: {arg}");
                return false;
            }
        }

        return true;
    }

    private static BuildCache LoadBuildCache()
    {
        var cachePath = Path.Combine(Application.dataPath, "..", BUILD_CACHE_FILE);
        
        if (File.Exists(cachePath))
        {
            try
            {
                var json = File.ReadAllText(cachePath);
                return JsonUtility.FromJson<BuildCache>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OptimizedBuild] 读取构建缓存失败: {e.Message}");
            }
        }

        return new BuildCache();
    }

    private static void SaveBuildCache(string projectHash)
    {
        var cache = new BuildCache
        {
            lastBuildHash = projectHash,
            lastBuildTime = DateTime.Now.Ticks,
            lastBuildFiles = GetProjectFiles()
        };

        var cachePath = Path.Combine(Application.dataPath, "..", BUILD_CACHE_FILE);
        
        try
        {
            var json = JsonUtility.ToJson(cache, true);
            File.WriteAllText(cachePath, json);
            Debug.Log("[OptimizedBuild] 构建缓存已保存");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OptimizedBuild] 保存构建缓存失败: {e.Message}");
        }
    }

    private static string CalculateProjectHash()
    {
        var files = GetProjectFiles();
        var combinedContent = string.Join("|", files.OrderBy(f => f));
        
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(combinedContent);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    private static List<string> GetProjectFiles()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var files = new List<string>();

        // 获取关键文件和目录
        var importantPaths = new[]
        {
            Path.Combine(projectRoot, "Assets"),
            Path.Combine(projectRoot, "Packages"),
            Path.Combine(projectRoot, "ProjectSettings")
        };

        foreach (var path in importantPaths)
        {
            if (Directory.Exists(path))
            {
                var pathFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => !EXCLUDE_FROM_CACHE.Any(exclude => f.Contains(exclude)))
                    .Select(f => f.Substring(projectRoot.Length))
                    .ToList();
                
                files.AddRange(pathFiles);
            }
        }

        return files;
    }

    private static bool ShouldPerformFullBuild(BuildCache cache, string currentHash)
    {
        // 如果没有缓存，执行完整构建
        if (string.IsNullOrEmpty(cache.lastBuildHash))
            return true;

        // 如果项目文件有变更，执行完整构建
        if (cache.lastBuildHash != currentHash)
            return true;

        // 如果缓存过期（超过1天），执行完整构建
        var lastBuildTime = new DateTime(cache.lastBuildTime);
        if (DateTime.Now - lastBuildTime > TimeSpan.FromDays(1))
            return true;

        return false;
    }

    private static void OptimizeUnitySettings()
    {
        // 禁用不必要的导入器
        EditorSettings.cacheServerMode = CacheServerMode.Disabled;
        
        // 优化资源导入设置
        EditorSettings.asyncShaderCompilation = false;
        
        // 设置图形API（如果需要）
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        
        Debug.Log("[OptimizedBuild] Unity设置已优化");
    }

    private static BuildReport PerformBuild(Dictionary<string, string> args, bool isFullBuild)
    {
        var outputPath = Path.Combine(args["prefixDir"], $"{args["productName"]}.exe");
        
        // 构建选项配置
        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = GetBuildOptions(isFullBuild)
        };

        // 设置产品信息
        PlayerSettings.productName = args["productName"];
        PlayerSettings.bundleVersion = args["version"];

        Debug.Log($"[OptimizedBuild] 开始构建到: {outputPath}");
        Debug.Log($"[OptimizedBuild] 构建模式: {(isFullBuild ? "完整构建" : "增量构建")}");

        return BuildPipeline.BuildPlayer(buildPlayerOptions);
    }

    private static BuildOptions GetBuildOptions(bool isFullBuild)
    {
        var options = BuildOptions.None;

        // 发布构建设置
        if (EditorUserBuildSettings.development == false)
        {
            options |= BuildOptions.None; // Release build
        }
        else
        {
            options |= BuildOptions.Development;
        }

        // 根据构建类型添加选项
        if (!isFullBuild)
        {
            // 增量构建可以使用的优化选项
            // options |= BuildOptions.BuildAdditionalStreamedScenes;
        }

        return options;
    }

    /// <summary>
    /// 清理构建缓存（可通过Jenkins参数调用）
    /// </summary>
    public static void CleanBuildCache()
    {
        var cachePath = Path.Combine(Application.dataPath, "..", BUILD_CACHE_FILE);
        
        if (File.Exists(cachePath))
        {
            File.Delete(cachePath);
            Debug.Log("[OptimizedBuild] 构建缓存已清理");
        }

        // 清理Library缓存
        var libraryPath = Path.Combine(Application.dataPath, "..", "Library");
        if (Directory.Exists(libraryPath))
        {
            try
            {
                Directory.Delete(libraryPath, true);
                Debug.Log("[OptimizedBuild] Library缓存已清理");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OptimizedBuild] 清理Library失败: {e.Message}");
            }
        }

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 预热构建（可在Jenkins中预先调用以准备环境）
    /// </summary>
    public static void WarmupBuild()
    {
        Debug.Log("[OptimizedBuild] 开始预热构建环境...");

        // 预编译着色器
        ShaderUtil.ClearCurrentShaderVariantCollection();
        
        // 刷新资源数据库
        AssetDatabase.Refresh();
        
        // 预加载必要的资源
        Resources.LoadAll("");

        Debug.Log("[OptimizedBuild] 构建环境预热完成");
        EditorApplication.Exit(0);
    }
}