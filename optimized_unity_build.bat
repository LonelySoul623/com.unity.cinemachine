@echo off
setlocal enabledelayedexpansion

:: ==== 性能监控开始 ====
set START_TIME=%TIME%
echo [INFO] 构建开始时间: %START_TIME%

set BRANCH=%this_branch%
echo [CONFIRM] 构建分支: %BRANCH%

echo [DEBUG] Jenkins参数 this_branch=%this_branch%
echo [DEBUG] 映射后的 BRANCH=%BRANCH%

if "%AUTO_job_projectVersion%"=="" (
    set "AUTO_job_projectVersion=1.0.0"
    echo [WARN] 使用默认版本: %AUTO_job_projectVersion%
)

echo [CONFIRM] 构建分支: %BRANCH% 版本: %AUTO_job_projectVersion%

:: ==== 硬编码路径 ====
set "WORKSPACE=D:\jenkins\workspace\workspace\bpc_xr_innovation13340"
set "UNITY_PATH=D:\Unity Hub\Editor\2021.2.8f1c1\Editor\Unity.exe"
set "PROJECT_PATH=%WORKSPACE%\pj_xr_holographic"
set VERSION=%AUTO_job_projectVersion%

:: ==== 缓存目录设置 ====
set "CACHE_DIR=%WORKSPACE%\.build_cache"
set "LIBRARY_CACHE=%CACHE_DIR%\Library"
set "PACKAGES_CACHE=%CACHE_DIR%\Packages"

:: ==== 动态生成输出路径 ====
set "processed_branch=%BRANCH:/=_%"
set "RESULT_DIR=%WORKSPACE%\origin\%processed_branch%_%AUTO_job_projectVersion%"
set "ZIP_PATH=%RESULT_DIR%.zip"

:: ==== 调试信息输出 ====
echo [DEBUG] 工作目录: %WORKSPACE%
echo [DEBUG] 输出目录: %RESULT_DIR%
echo [DEBUG] 压缩包路径: %ZIP_PATH%
echo [DEBUG] 缓存目录: %CACHE_DIR%

:: ==== 初始化目录（强制创建） ====
mkdir "%WORKSPACE%\origin" >nul 2>&1
mkdir "%RESULT_DIR%" >nul 2>&1
mkdir "%CACHE_DIR%" >nul 2>&1
mkdir "%LIBRARY_CACHE%" >nul 2>&1
mkdir "%PACKAGES_CACHE%" >nul 2>&1

if not exist "%RESULT_DIR%" (
    echo [ERROR] 无法创建输出目录: %RESULT_DIR%
    echo [DEBUG] 当前权限:
    icacls "%WORKSPACE%" | findstr /i "SYSTEM Everyone"
    exit /b 1
)

:: ==== 优化的Git同步操作 ====
echo [INFO] 开始Git同步优化...
if exist ".git" (
    :: 检查当前分支和远程状态
    for /f "tokens=*" %%i in ('git rev-parse --abbrev-ref HEAD') do set CURRENT_BRANCH=%%i
    for /f "tokens=*" %%i in ('git rev-parse HEAD') do set LOCAL_COMMIT=%%i
    
    :: 只在必要时进行fetch
    git ls-remote --heads origin %BRANCH% > temp_remote_info.txt
    for /f "tokens=1" %%i in ('type temp_remote_info.txt') do set REMOTE_COMMIT=%%i
    del temp_remote_info.txt
    
    if "!LOCAL_COMMIT!"=="!REMOTE_COMMIT!" (
        echo [INFO] 代码已是最新，跳过Git同步
    ) else (
        echo [INFO] 检测到代码变更，执行增量同步...
        git fetch origin %BRANCH% --depth=1
        if "!CURRENT_BRANCH!"=="%BRANCH%" (
            git reset --hard origin/%BRANCH%
        ) else (
            git checkout -f %BRANCH%
            git reset --hard origin/%BRANCH%
        )
    )
) else (
    echo [WARN] 未找到Git仓库，跳过代码同步
)

:: ==== 验证Unity项目 ====
if not exist "%PROJECT_PATH%\Assets" (
    echo [ERROR] Unity项目路径无效: %PROJECT_PATH%
    dir /b "%WORKSPACE%"
    exit /b 1
)

:: ==== Library缓存恢复 ====
echo [INFO] 检查Library缓存...
if exist "%LIBRARY_CACHE%\metadata" (
    if not exist "%PROJECT_PATH%\Library\metadata" (
        echo [INFO] 恢复Library缓存...
        xcopy "%LIBRARY_CACHE%\*" "%PROJECT_PATH%\Library\" /E /I /Y >nul 2>&1
    )
) else (
    echo [INFO] 首次构建，将创建Library缓存
)

:: ==== Packages缓存恢复 ====
echo [INFO] 检查Packages缓存...
if exist "%PACKAGES_CACHE%\manifest.json" (
    xcopy "%PACKAGES_CACHE%\*" "%PROJECT_PATH%\Packages\" /E /I /Y >nul 2>&1
)

:: ==== 执行优化构建 ====
echo [INFO] 开始优化构建，输出目录: %RESULT_DIR%
"%UNITY_PATH%" ^
  -batchmode ^
  -quit ^
  -nographics ^
  -silent-crashes ^
  -disable-assembly-updater ^
  -accept-apiupdate ^
  -buildTarget Win64 ^
  -projectPath "%PROJECT_PATH%" ^
  -executeMethod BuilldPlayerTools.BuildPlayer ^
  -productName=Holo ^
  -version=%VERSION% ^
  -prefixDir="%RESULT_DIR%" ^
  -logFile "%WORKSPACE%\unity_build.log"

:: ==== 检查结果 ====
if %errorlevel% neq 0 (
    echo [ERROR] Unity构建失败！详见日志:
    type "%WORKSPACE%\unity_build.log" | findstr /i "error fail exception"
    exit /b 1
)

if not exist "%RESULT_DIR%\Holo.exe" (
    echo [ERROR] 未生成输出文件: %RESULT_DIR%\Holo.exe
    exit /b 1
)

:: ==== Library缓存保存 ====
echo [INFO] 保存Library缓存...
if exist "%PROJECT_PATH%\Library\metadata" (
    xcopy "%PROJECT_PATH%\Library\*" "%LIBRARY_CACHE%\" /E /I /Y >nul 2>&1
)

:: ==== Packages缓存保存 ====
echo [INFO] 保存Packages缓存...
if exist "%PROJECT_PATH%\Packages\manifest.json" (
    xcopy "%PROJECT_PATH%\Packages\*" "%PACKAGES_CACHE%\" /E /I /Y >nul 2>&1
)

:: ==== 性能监控结束 ====
set END_TIME=%TIME%
echo [INFO] 构建结束时间: %END_TIME%

:: 计算构建时间（简化版本）
echo [SUCCESS] 构建成功: %RESULT_DIR%\Holo.exe
echo [INFO] 构建开始: %START_TIME%
echo [INFO] 构建结束: %END_TIME%
echo [INFO] 缓存目录: %CACHE_DIR%