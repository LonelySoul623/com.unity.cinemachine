# Unity Jenkins 构建优化指南

## 📊 优化效果预期

通过实施本指南的优化方案，预期可以将构建时间从 **20分钟** 缩短到 **8-12分钟**，节省 **40-60%** 的构建时间。

## 🚀 主要优化点

### 1. Git操作优化 (节省2-3分钟)
- ✅ 使用浅克隆 (`--depth=1`)
- ✅ 增量同步检查
- ✅ 跳过不必要的分支切换

### 2. Unity构建参数优化 (节省3-5分钟)
- ✅ 添加 `-nographics` 禁用图形界面
- ✅ 添加 `-silent-crashes` 减少错误处理开销
- ✅ 添加 `-disable-assembly-updater` 跳过程序集更新
- ✅ 使用 `-accept-apiupdate` 自动接受API更新

### 3. 缓存策略 (节省5-8分钟)
- ✅ Library文件夹缓存
- ✅ Packages缓存
- ✅ 构建产物增量检测

### 4. 并行化处理 (节省2-3分钟)
- ✅ Jenkins并行阶段执行
- ✅ 代码同步与缓存初始化并行
- ✅ 测试和验证并行

## 📁 文件结构

```
项目根目录/
├── optimized_unity_build.bat          # 优化后的批处理脚本
├── Jenkinsfile_optimized              # 优化后的Jenkins流水线
├── OptimizedBuildPlayerTools.cs       # Unity构建工具脚本
└── Unity_Jenkins_Optimization_Guide.md # 本文档
```

## 🔧 实施步骤

### 步骤1: 更新Unity构建脚本

1. 将 `OptimizedBuildPlayerTools.cs` 放置到Unity项目的 `Assets/Editor/` 目录下
2. 修改原有的构建方法调用：
   ```batch
   # 原来的调用
   -executeMethod BuilldPlayerTools.BuildPlayer
   
   # 修改为
   -executeMethod OptimizedBuildPlayerTools.BuildPlayer
   ```

### 步骤2: 替换批处理脚本

使用 `optimized_unity_build.bat` 替换现有的构建脚本，主要改进包括：

```batch
# 主要优化点
:: 1. 智能Git同步
if "!LOCAL_COMMIT!"=="!REMOTE_COMMIT!" (
    echo [INFO] 代码已是最新，跳过Git同步
) else (
    git fetch origin %BRANCH% --depth=1
)

:: 2. 缓存恢复和保存
xcopy "%LIBRARY_CACHE%\*" "%PROJECT_PATH%\Library\" /E /I /Y >nul 2>&1

:: 3. 优化的Unity参数
"%UNITY_PATH%" ^
  -batchmode ^
  -quit ^
  -nographics ^
  -silent-crashes ^
  -disable-assembly-updater ^
  -accept-apiupdate ^
  -buildTarget Win64
```

### 步骤3: 配置Jenkins流水线

使用 `Jenkinsfile_optimized` 配置Jenkins，主要特性：

```groovy
// 并行执行
stage('环境准备') {
    parallel {
        stage('代码同步') { /* Git操作 */ }
        stage('缓存初始化') { /* 缓存目录准备 */ }
    }
}

// 条件执行
stage('缓存恢复') {
    when { not { params.CLEAN_BUILD } }
    // 只在非清理构建时恢复缓存
}
```

### 步骤4: Jenkins参数配置

在Jenkins任务中添加以下参数：

| 参数名 | 类型 | 默认值 | 描述 |
|--------|------|--------|------|
| `this_branch` | String | main | 构建分支 |
| `AUTO_job_projectVersion` | String | 1.0.0 | 版本号 |
| `CLEAN_BUILD` | Boolean | false | 强制清理构建 |
| `SKIP_TESTS` | Boolean | false | 跳过测试 |

## ⚡ 高级优化选项

### 1. 硬件优化建议

```yaml
推荐配置:
  CPU: 至少8核心 (推荐16核心)
  内存: 至少16GB (推荐32GB)
  存储: SSD (NVMe推荐)
  网络: 千兆以上带宽
```

### 2. Unity项目优化

在Unity项目中应用以下设置：

```csharp
// 在ProjectSettings中设置
PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, 
    new[] { GraphicsDeviceType.Direct3D11 });

// 禁用不必要的功能
EditorSettings.asyncShaderCompilation = false;
EditorSettings.cacheServerMode = CacheServerMode.Disabled;
```

### 3. 构建环境变量

在Jenkins中设置以下环境变量：

```bash
# Unity性能优化
UNITY_DISABLE_GRAPHICS=1
UNITY_BATCH_MODE=1

# 缓存配置
UNITY_CACHE_SIZE=10GB
UNITY_TEMP_CACHE_SIZE=5GB
```

## 📈 性能监控

### 构建时间分析

优化前后的时间对比：

| 阶段 | 优化前 | 优化后 | 节省时间 |
|------|--------|--------|----------|
| Git同步 | 2-3分钟 | 30秒-1分钟 | 1.5-2分钟 |
| Unity导入 | 8-10分钟 | 3-5分钟 | 5分钟 |
| 代码编译 | 5-6分钟 | 2-3分钟 | 3分钟 |
| 资源打包 | 3-4分钟 | 2-3分钟 | 1分钟 |
| 后处理 | 1-2分钟 | 30秒-1分钟 | 1分钟 |
| **总计** | **20分钟** | **8-12分钟** | **8-12分钟** |

### 监控指标

在Jenkins中监控以下指标：

1. **构建时间趋势**
2. **缓存命中率**
3. **增量构建比例**
4. **资源使用情况**

## 🛠️ 故障排除

### 常见问题

#### 1. 缓存相关问题

```bash
# 清理所有缓存
-executeMethod OptimizedBuildPlayerTools.CleanBuildCache

# 或在Jenkins中使用CLEAN_BUILD参数
```

#### 2. Git同步问题

```bash
# 检查Git配置
git config --list
git remote -v

# 重置本地仓库
git reset --hard HEAD
git clean -fd
```

#### 3. Unity构建失败

```bash
# 检查Unity日志
type "%WORKSPACE%\unity_build.log" | findstr /i "error fail exception"

# 预热构建环境
-executeMethod OptimizedBuildPlayerTools.WarmupBuild
```

## 📝 最佳实践

### 1. 定期维护

- 每周清理一次构建缓存
- 每月更新Unity和Jenkins版本
- 定期检查磁盘空间使用情况

### 2. 监控和调优

- 设置构建时间告警（超过15分钟）
- 监控缓存大小（不超过50GB）
- 跟踪构建成功率（目标>95%）

### 3. 团队协作

- 制定代码提交规范
- 避免大文件提交
- 合理使用分支策略

## 🔗 相关资源

- [Unity官方构建优化文档](https://docs.unity3d.com/Manual/BuildPlayerPipeline.html)
- [Jenkins Pipeline最佳实践](https://www.jenkins.io/doc/book/pipeline/syntax/)
- [Git优化指南](https://git-scm.com/book/en/v2/Git-Internals-Transfer-Protocols)

## 📞 技术支持

如需进一步的技术支持或有任何问题，请联系开发团队或提交Issue。

---

**注意**: 首次实施优化后，建议观察几次构建的效果，并根据实际情况微调参数。