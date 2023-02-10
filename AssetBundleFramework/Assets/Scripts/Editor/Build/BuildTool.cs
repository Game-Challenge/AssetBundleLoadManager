﻿/*
 * Description:             BuildTool.cs
 * Author:                  TONYTANG
 * Create Date:             2021//12/19
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TResource;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BuildTool.cs
/// 打包工具
/// </summary>
public static class BuildTool 
{
    /// <summary>
    /// 修改包内游戏开发模式信息
    /// </summary>
    /// <param name="developMode">游戏开发模式</param>
    public static bool ModifyInnerGameConfig(GameDevelopMode developMode)
    {
        if (developMode == GameDevelopMode.Invalide)
        {
            Debug.LogError($"不支持的游戏开发模式:{developMode},请传入输入有效游戏开发模式，修改游戏开发模式失败!");
            return false;
        }

        GameConfigModuleManager.Singleton.initGameConfigData();
        var gameDevelopMode = GameConfigModuleManager.Singleton.GetGameDevelopMode();
        Debug.Log($"包内游戏开发模式从:{gameDevelopMode}修改成:{developMode}");
        GameConfigModuleManager.Singleton.saveGameDevelopModel(developMode);
        return true;
    }

    /// <summary>
    /// 修改包内版本信息
    /// </summary>
    /// <param name="versionCode">新的版本号</param>
    /// <param name="resourceVersionCode">新的资源版本号</param>
    public static bool ModifyInnerVersionConfig(double versionCode, int resourceVersionCode)
    {
        // 版本号格式只允许*.*
        var versionString = versionCode.ToString("N1", CultureInfo.CreateSpecificCulture("en-US"));
        if (!double.TryParse(versionString, out versionCode))
        {
            Debug.LogError($"不支持的版本号:{versionCode},请传入输入有效版本号值!");
            return false;
        }
        if(resourceVersionCode <= 0)
        {
            Debug.LogError($"不支持的资源版本号:{resourceVersionCode},请传入输入有效资源版本号值!");
            return false;
        }
        VersionConfigModuleManager.Singleton.initVerisonConfigData();
        var innerversioncode = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.VersionCode;
        var innerresourceversioncode = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.ResourceVersionCode;
        Debug.Log($"包内版本号从:{innerversioncode}修改成:{versionCode}");
        Debug.Log($"包内资源版本号从:{innerresourceversioncode}修改成:{resourceVersionCode}");
        VersionConfigModuleManager.Singleton.saveNewVersionCodeInnerConfig(versionCode);
        VersionConfigModuleManager.Singleton.saveNewResoueceCodeInnerConfig(resourceVersionCode);
        return true;
    }

    /// <summary>
    /// 执行打包
    /// </summary>
    /// <param name="buildOutputPath">打包输出目录</param>
    /// <param name="buildTarget">打包平台</param>
    /// <param name="versionCode">版本号</param>
    /// <param name="resourceVersionCode">资源版本号</param>
    /// <param name="isDevelopment">是否打开发包</param>
    public static void DoBuild(string buildOutputPath, BuildTarget buildTarget, double versionCode, int resourceVersionCode, bool isDevelopment = false)
    {
        Debug.Log("BuildTool.DoBuild()");
        // 版本号格式只允许*.*
        var versionString = versionCode.ToString("N1", CultureInfo.CreateSpecificCulture("en-US"));
        if (!double.TryParse(versionString, out versionCode))
        {
            Debug.LogError($"不支持的版本号:{versionCode},请传入输入有效版本号值!");
            return;
        }
        if (string.IsNullOrEmpty(buildOutputPath))
        {
            buildOutputPath = $"{Application.dataPath}/../../../Build";
        }
        // 输出目录结构:Build/版本号/资源版本号/时间戳/包名.apk
        var now = DateTime.Now;
        var timeStamp = $"{now.Year}_{now.Month}_{now.Day}_{now.Hour}_{now.Minute}_{now.Second}";
        buildOutputPath = $"{buildOutputPath}/{versionCode}/{resourceVersionCode}/{timeStamp}";
        Debug.Log($"buildOutputPath:{buildOutputPath}");
        if (!string.IsNullOrEmpty(buildOutputPath))
        {
            if (!Directory.Exists(buildOutputPath))
            {
                Directory.CreateDirectory(buildOutputPath);
            }
            var buildtargetgroup = GetCorrespondingBuildTaregtGroup(buildTarget);
            Debug.Log($"打包分组:{Enum.GetName(typeof(BuildTargetGroup), buildtargetgroup)}");
            if (buildtargetgroup != BuildTargetGroup.Unknown)
            {
                VersionConfigModuleManager.Singleton.initVerisonConfigData();
                var innerversioncode = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.VersionCode;
                var innerresourceversioncode = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.ResourceVersionCode;
                Debug.Log("打包版本信息:");
                Debug.Log($"版本号:{versionCode} 资源版本号:{resourceVersionCode}");
                Debug.Log($"包内VersionConfig信息:");
                Debug.Log($"版本号:{innerversioncode} 资源版本号:{innerresourceversioncode}");
                var prebundleversion = PlayerSettings.bundleVersion;
                PlayerSettings.bundleVersion = versionCode.ToString();
                Debug.Log($"打包修改版本号从:{prebundleversion}到{PlayerSettings.bundleVersion}");
                Debug.Log($"打包修改VersionConfig从:Version:{innerversioncode}到{versionCode} ResourceVersion:{innerresourceversioncode}到{resourceVersionCode}");
                VersionConfigModuleManager.Singleton.saveNewVersionCodeInnerConfig(versionCode);
                VersionConfigModuleManager.Singleton.saveNewResoueceCodeInnerConfig(resourceVersionCode);
                BuildPlayerOptions buildplayeroptions = new BuildPlayerOptions();
                buildplayeroptions.locationPathName = $"{buildOutputPath}{Path.DirectorySeparatorChar}{PlayerSettings.productName}{GetCorrespondingBuildFilePostfix(buildTarget)}";
                buildplayeroptions.scenes = GetBuildSceneArray();
                buildplayeroptions.target = buildTarget;
                buildplayeroptions.options = BuildOptions.StrictMode;
                if(isDevelopment)
                {
                    buildplayeroptions.options |= BuildOptions.Development;
                }
                Debug.Log($"打包平台:{Enum.GetName(typeof(BuildTarget), buildTarget)}");
                Debug.Log($"开发版本:{isDevelopment}");
                Debug.Log($"打包输出路径:{buildplayeroptions.locationPathName}");
                buildplayeroptions.targetGroup = buildtargetgroup;
                EditorUserBuildSettings.SwitchActiveBuildTarget(buildtargetgroup, buildTarget);
                BuildPipeline.BuildPlayer(buildplayeroptions);
                // 拷贝AssetBundleMd5.txt到打包输出目录(未来热更新对比需要的文件)
                var innerAssetBundleMd5FilePath = AssetBundlePath.GetInnerAssetBundleMd5FilePath();
                FileUtilities.CopyFileToFolder(innerAssetBundleMd5FilePath, buildOutputPath);
            }
            else
            {
                Debug.LogError("不支持的打包平台选择,打包失败!");
            }
        }
        else
        {
            Debug.LogError("打包输出目录为空或不存在,打包失败!");
        }
    }

    /// <summary>
    /// 获取需要打包的场景数组
    /// </summary>
    /// <returns></returns>
    private static string[] GetBuildSceneArray()
    {
        //暂时默认BuildSetting里设置的场景才是要进包的场景
        List<string> editorscenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            editorscenes.Add(scene.path);
            Debug.Log($"需要打包的场景:{scene.path}");
        }
        return editorscenes.ToArray();
    }

    /// <summary>
    /// 获取对应的打包分组
    /// </summary>
    /// <param name="buildtarget"></param>
    /// <returns></returns>
    private static BuildTargetGroup GetCorrespondingBuildTaregtGroup(BuildTarget buildtarget)
    {
        switch (buildtarget)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return BuildTargetGroup.Standalone;
            case BuildTarget.Android:
                return BuildTargetGroup.Android;
            case BuildTarget.iOS:
                return BuildTargetGroup.iOS;
            default:
                return BuildTargetGroup.Unknown;
        }
    }

    /// <summary>
    /// 获取对应的打包分组的打包文件后缀
    /// </summary>
    /// <param name="buildtarget"></param>
    /// <returns></returns>
    private static string GetCorrespondingBuildFilePostfix(BuildTarget buildtarget)
    {
        switch (buildtarget)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return ".exe";
            case BuildTarget.Android:
                return ".apk";
            case BuildTarget.iOS:
                return "";
            default:
                return "";
        }
    }
}