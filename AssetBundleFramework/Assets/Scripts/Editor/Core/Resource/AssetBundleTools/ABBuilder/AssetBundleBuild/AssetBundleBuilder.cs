﻿//--------------------------------------------------
// Motion Framework
// Copyright©2018-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Security.Cryptography;
using TResource;

namespace MotionFramework.Editor
{
	public class AssetBundleBuilder
	{
		/// <summary>
		/// AssetBundle压缩选项
		/// </summary>
		public enum ECompressOption
		{
			Uncompressed = 0,
			StandardCompressionLZMA,
			ChunkBasedCompressionLZ4,
		}

		/// <summary>
		/// 输出的根目录
		/// </summary>
		private readonly string _outputRoot;

		/// <summary>
		/// 构建平台
		/// </summary>
		public BuildTarget BuildTarget { private set; get; } = BuildTarget.NoTarget;

		/// <summary>
		/// 输出目录
		/// </summary>
		public string OutputDirectory { private set; get; } = string.Empty;

		/// <summary>
		/// 构建选项
		/// </summary>
		public ECompressOption CompressOption = ECompressOption.Uncompressed;

		/// <summary>
		/// 是否强制重新打包资源
		/// </summary>
		public bool IsForceRebuild = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsAppendHash = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsDisableWriteTypeTree = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsIgnoreTypeTreeChanges = false;

		/// <summary>
		/// 所有Asset信息Map<Asset路径, Asset信息>(避免相同Asset重复New AssetInfo)
		/// </summary>
		private Dictionary<string, AssetInfo> AllAssetInfoMap = new Dictionary<string, AssetInfo>();

		/// <summary>
		/// AssetBuilder
		/// </summary>
		/// <param name="buildTarget">构建平台</param>
		public AssetBundleBuilder(BuildTarget buildTarget)
		{
			_outputRoot = AssetBundleBuilderHelper.GetDefaultOutputRootPath();
			BuildTarget = buildTarget;
			OutputDirectory = GetOutputDirectory();
		}

		/// <summary>
		/// 准备构建
		/// </summary>
		public void PreAssetBuild()
		{
			Debug.Log("------------------------------OnPreAssetBuild------------------------------");

			// 检测构建平台是否合法
			if (BuildTarget == BuildTarget.NoTarget)
				throw new Exception("[BuildPatch] 请选择目标平台");

			// 检测构建版本是否合法
			//if (EditorUtilities.IsNumber(BuildVersion.ToString()) == false)
			//	throw new Exception($"[BuildPatch] 版本号格式非法：{BuildVersion}");
			//if (BuildVersion < 0)
			//	throw new Exception("[BuildPatch] 请先设置版本号");

			// 检测输出目录是否为空
			if (string.IsNullOrEmpty(OutputDirectory))
				throw new Exception("[BuildPatch] 输出目录不能为空");

			// 检测补丁包是否已经存在
			//string packageDirectory = GetPackageDirectory();
			//if (Directory.Exists(packageDirectory))
			//	throw new Exception($"[BuildPatch] 补丁包已经存在：{packageDirectory}");

			// 如果是强制重建
			if (IsForceRebuild)
			{
				// 删除平台总目录
				string platformDirectory = $"{_outputRoot}/{BuildTarget}";
				if (Directory.Exists(platformDirectory))
				{
					Directory.Delete(platformDirectory, true);
					Log($"删除平台总目录：{platformDirectory}");
				}
			}

			// 如果输出目录不存在
			if (Directory.Exists(OutputDirectory) == false)
			{
				Directory.CreateDirectory(OutputDirectory);
				Log($"创建输出目录：{OutputDirectory}");
			}

            // Asset打包信息输出目录不存在
            var assetbuildinfofolderpath = GetAssetBuildInfoFolderFullPath();
            Debug.Log($"Asset打包信息输出目录:{assetbuildinfofolderpath}");
            if (!Directory.Exists(assetbuildinfofolderpath))
            {
                Directory.CreateDirectory(assetbuildinfofolderpath);
                Log($"创建打包信息Asset输出目录：{assetbuildinfofolderpath}");
            }
		}

		/// <summary>
		/// 执行构建
		/// </summary>
		public void PostAssetBuild()
		{
			Debug.Log("------------------------------OnPostAssetBuild------------------------------");

			// 准备工作
			List<AssetBundleBuild> buildInfoList = new List<AssetBundleBuild>();
			List<AssetInfo> buildAssetInfoList = GetBuildAssetInfoList();
			if (buildAssetInfoList.Count == 0)
				throw new Exception("[BuildPatch] 构建列表不能为空");

			Log($"构建列表里总共有{buildAssetInfoList.Count}个资源需要构建");
            // 问题: 
            // 2020和2021版本传递多个相同assetBundleName的AssetBundleBuild信息会报错
            // [BuildPipeline error is thrown when building Asset Bundles](https://issuetracker.unity3d.com/issues/buildpipeline-error-is-thrown-when-building-asset-bundles)
            // 修复(2022/06/03):
            // 基于基础的分析结论将同assetBundleName的Asset信息设置到同一个AssetBundleBuild里
            // Note:
            // 不支持变体功能，变体功能只用于区分不同平台的AB包后缀
            // asset bundle信息Map<assetBundle名, <AssetBundle变体名, Asset信息列表>>
            Dictionary<string, Dictionary<string, List<AssetInfo>>> assetBundleInfoMap = new Dictionary<string, Dictionary<string, List<AssetInfo>>>();
			for (int i = 0; i < buildAssetInfoList.Count; i++)
			{
				AssetInfo assetInfo = buildAssetInfoList[i];
				if (!assetBundleInfoMap.ContainsKey(assetInfo.AssetBundleLabel))
                {
					assetBundleInfoMap[assetInfo.AssetBundleLabel] = new Dictionary<string, List<AssetInfo>>();
				}
                if(!assetBundleInfoMap[assetInfo.AssetBundleLabel].ContainsKey(assetInfo.AssetBundleVariant))
                {
                    assetBundleInfoMap[assetInfo.AssetBundleLabel][assetInfo.AssetBundleVariant] = new List<AssetInfo>();
                }
                assetBundleInfoMap[assetInfo.AssetBundleLabel][assetInfo.AssetBundleVariant].Add(assetInfo);
			}
			var assetNameList = new List<string>();
			foreach(var assetBundleInfo in assetBundleInfoMap)
            {
                foreach (var assetBundleVariantInfo in assetBundleInfo.Value)
                {
                    AssetBundleBuild buildInfo = new AssetBundleBuild();
                    buildInfo.assetBundleName = assetBundleInfo.Key;
                    buildInfo.assetBundleVariant = assetBundleVariantInfo.Key;
					assetNameList.Clear();
					foreach(var assetInfoList in assetBundleVariantInfo.Value)
                    {
						assetNameList.Add(assetInfoList.AssetPath);
					}
					buildInfo.assetNames = assetNameList.ToArray();
					buildInfoList.Add(buildInfo);
                }
            }
            // AssetBuildInfoAsset打包信息单独打包
            var assetbuildinfoassetrelativepath = GetAssetBuildInfoFileRelativePath();
            var buildinfo = new AssetBundleBuild();
            buildinfo.assetBundleName = GetAssetBuildInfoAssetName();
			buildinfo.assetBundleVariant = GetBuildAssetBundlePostFix();
			buildinfo.assetNames = new string[] { assetbuildinfoassetrelativepath };
            buildInfoList.Add(buildinfo);

            // 更新AB打包信息Asset(e.g.比如Asset打包信息)
            UpdateAssetBundleBuildInfoAsset(buildInfoList);
			buildInfoList.Sort((assetBundleBuild1, assetBundleBuild2) => {
				return assetBundleBuild1.assetBundleName.CompareTo(assetBundleBuild2.assetBundleName);
			});

			// 创建Asset AB打包详细说明信息文件
			CreateAssetBuildReadmeFile(buildInfoList);

            // 开始构建
            Log($"开始构建......");
			BuildAssetBundleOptions opt = MakeBuildOptions();
			AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(OutputDirectory, buildInfoList.ToArray(), opt, BuildTarget);
			if (unityManifest == null)
				throw new Exception("[BuildPatch] 构建过程中发生错误！");

            // 1. 检测循环依赖
            CheckCycleDepend(unityManifest);
            // 3. 创建说明文件
            CreateReadmeFile(unityManifest);
            // 视频单独打包
            PackVideo(buildAssetInfoList);
            // 单独生成包内的AssetBundle的MD5信息(用于热更新判定)
            CreateAssetBundleMd5InfoFile();

            Log("构建完成！");
		}

        /// <summary>
        /// 更新AssetBundle打包编译信息Asset
        /// </summary>
        /// <param name="buildinfolist"></param>
        private void UpdateAssetBundleBuildInfoAsset(List<AssetBundleBuild> buildinfolist)
        {
            // Note: AssetBundle打包信息统一存小写，确保和AB打包那方一致
            var assetbundlebuildinfoassetrelativepath = GetAssetBuildInfoFileRelativePath();
            var assetbundlebuildasset = AssetDatabase.LoadAssetAtPath<AssetBuildInfoAsset>(assetbundlebuildinfoassetrelativepath);
            if (assetbundlebuildasset == null)
            {
                assetbundlebuildasset = new AssetBuildInfoAsset();
                AssetDatabase.CreateAsset(assetbundlebuildasset, assetbundlebuildinfoassetrelativepath);
            }
            assetbundlebuildasset.AssetBuildInfoList.Clear();

            // Asset打包信息构建
            foreach (var bi in buildinfolist)
            {
				foreach(var assetName in bi.assetNames)
                {
                    var abbi = new AssetBuildInfo();
                    // 不剔除后缀，确保AssetDatabase模式可以全路径(带后缀)加载
                    abbi.AssetPath = assetName.ToLower();//.Substring(0, bi.assetNames[0].Length - Path.GetExtension(bi.assetNames[0]).Length).ToLower();
                    abbi.ABPath = bi.assetBundleName.ToLower();
                    abbi.ABVariantPath = bi.assetBundleVariant != null ? bi.assetBundleVariant.ToLower() : null;
                    assetbundlebuildasset.AssetBuildInfoList.Add(abbi);
                }
            }

            EditorUtility.SetDirty(assetbundlebuildasset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 获取构建选项
        /// </summary>
        private BuildAssetBundleOptions MakeBuildOptions()
		{
			// For the new build system, unity always need BuildAssetBundleOptions.CollectDependencies and BuildAssetBundleOptions.DeterministicAssetBundle
			// 除非设置ForceRebuildAssetBundle标记，否则会进行增量打包

			BuildAssetBundleOptions opt = BuildAssetBundleOptions.None;
			opt |= BuildAssetBundleOptions.StrictMode; //Do not allow the build to succeed if any errors are reporting during it.

			if (CompressOption == ECompressOption.Uncompressed)
				opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
			else if (CompressOption == ECompressOption.ChunkBasedCompressionLZ4)
				opt |= BuildAssetBundleOptions.ChunkBasedCompression;

			if (IsForceRebuild)
				opt |= BuildAssetBundleOptions.ForceRebuildAssetBundle; //Force rebuild the asset bundles
			if (IsAppendHash)
				opt |= BuildAssetBundleOptions.AppendHashToAssetBundleName; //Append the hash to the assetBundle name
			if (IsDisableWriteTypeTree)
				opt |= BuildAssetBundleOptions.DisableWriteTypeTree; //Do not include type information within the asset bundle (don't write type tree).
			if (IsIgnoreTypeTreeChanges)
				opt |= BuildAssetBundleOptions.IgnoreTypeTreeChanges; //Ignore the type tree changes when doing the incremental build check.

			return opt;
		}

		private void Log(string log)
		{
			Debug.Log($"[BuildPatch] {log}");
		}
		private string GetOutputDirectory()
		{
            return $"{_outputRoot}/{BuildTarget}/";
            //{AssetBundleBuildConstData.UnityManifestFileName}";
		}
		private string GetPackageDirectory()
		{
			return $"{_outputRoot}/{BuildTarget}/";
		}

        /// <summary>
        /// 获取Asset打包信息Asset所在目录全路径
        /// </summary>
        /// <returns></returns>
        private string GetAssetBuildInfoFolderFullPath()
        {
            return $"{Application.dataPath}/{ResourceConstData.AssetBuildInfoAssetRelativePath}";
        }

        /// <summary>
        /// 获取Asset打包信息文件相对路径
        /// </summary>
        /// <returns></returns>
        private string GetAssetBuildInfoFileRelativePath()
        {
            return $"Assets/{ResourceConstData.AssetBuildInfoAssetRelativePath}/{GetAssetBuildInfoAssetName()}.asset";
        }

		/// <summary>
		/// 清理Asset信息
		/// </summary>
		private void ClearAssetInfos()
        {
			AllAssetInfoMap.Clear();
		}

		/// <summary>
		/// 获取Asset信息
		/// </summary>
		/// <param name="assetPath"></param>
		/// <returns></returns>
		private AssetInfo GetAssetInfo(string assetPath)
        {
			AssetInfo assetInfo;
			if(!AllAssetInfoMap.TryGetValue(assetPath, out assetInfo))
            {
				return null;
            }
			return assetInfo;
        }

		/// <summary>
		/// 添加Asset信息
		/// </summary>
		/// <param name="assetInfo"></param>
		/// <returns></returns>
		private bool AddAssetInfo(AssetInfo assetInfo)
        {
			if(AllAssetInfoMap.ContainsKey(assetInfo.AssetPath))
            {
				Debug.LogError($"重复添加Asset路径:{assetInfo.AssetPath}的Asset信息，添加失败，请检查代码!");
				return false;
            }
			AllAssetInfoMap.Add(assetInfo.AssetPath, assetInfo);
			return true;
		}

        #region 准备工作
        /// <summary>
        /// 准备工作
        /// </summary>
        private List<AssetInfo> GetBuildAssetInfoList()
		{
			ClearAssetInfos();
			int progressBarCount = 0;

			// 获取所有的收集路径
			List<string> collectDirectorys = AssetBundleCollectSettingData.GetAllCollectDirectory();
			if (collectDirectorys.Count == 0)
				throw new Exception("[BuildPatch] 配置的资源收集路径为空");

			// 获取所有资源
			string[] guids = AssetDatabase.FindAssets(string.Empty, collectDirectorys.ToArray());
			foreach (string guid in guids)
			{
				string mainAssetPath = AssetDatabase.GUIDToAssetPath(guid);
				string regularMainAssetPath = PathUtilities.GetRegularPath(mainAssetPath);
				if (ValidateAsset(regularMainAssetPath) == false)
                {
					continue;
                }
				UpdateAssetAllAssetInfo(regularMainAssetPath);
				// 进度条
				progressBarCount++;
				EditorUtility.DisplayProgressBar("进度", $"依赖文件分析：{progressBarCount}/{guids.Length}", (float)progressBarCount / guids.Length);
			}
			EditorUtility.ClearProgressBar();
			progressBarCount = 0;

			/* 
			// 零依赖资源可能是纯动态加载的资源，是有可能用到的需要参与打包
			// 移除零依赖的资源
			List<string> removeList = new List<string>();
			foreach (KeyValuePair<string, AssetInfo> pair in AllAssetInfoMap)
			{
				if (pair.Value.IsCollectAsset)
					continue;
				if (pair.Value.DependCount == 0)
					removeList.Add(pair.Value.AssetPath);
			}
			for (int i = 0; i < removeList.Count; i++)
			{
				allAsset.Remove(removeList[i]);
                Debug.Log($"移除零依赖资源:{removeList[i]}");
			}
			*/

            // 设置资源标签
            var totalAssetNum = AllAssetInfoMap.Count;
			foreach (KeyValuePair<string, AssetInfo> pair in AllAssetInfoMap)
			{
				SetAssetBundleLabelAndVariant(pair.Value);

				// 进度条
				progressBarCount++;
				EditorUtility.DisplayProgressBar("进度", $"设置资源标签：{progressBarCount}/{totalAssetNum}", (float)progressBarCount / totalAssetNum);
			}

            EditorUtility.ClearProgressBar();

			// 返回结果
			return AllAssetInfoMap.Values.ToList();
		}

		/// <summary>
		/// 更新指定资源的所有Asset信息
		/// </summary>
		private void UpdateAssetAllAssetInfo(string assetPath)
		{
			/// 注意：返回列表里已经包括主资源自己
			string[] dependArray = AssetDatabase.GetDependencies(assetPath, true);
			foreach (string dependPath in dependArray)
			{
				var regularDependendPath = PathUtilities.GetRegularPath(dependPath);
				if (ValidateAsset(regularDependendPath))
				{
					var assetInfo = GetAssetInfo(regularDependendPath);
					if (assetInfo == null)
					{
						assetInfo = new AssetInfo(regularDependendPath);
						AddAssetInfo(assetInfo);
					}
					else
					{
						AllAssetInfoMap[regularDependendPath].DependCount++;
					}
				}
			}
		}

        /// <summary>
        /// 获取AB打包后缀名
        /// </summary>
        /// <returns></returns>
        private string GetBuildAssetBundlePostFix()
        {
            if (BuildTarget == BuildTarget.StandaloneWindows || BuildTarget == BuildTarget.StandaloneWindows64)
            {
                return AssetBundlePath.WindowAssetBundlePostFix;
            }
            if (BuildTarget == BuildTarget.Android)
            {
                return AssetBundlePath.AndroidAssetBundlePostFix;
            }
            if (BuildTarget == BuildTarget.iOS)
            {
                return AssetBundlePath.IOSAssetBundlePostFix;
            }
            else
            {
                Debug.LogError($"不支持的打包平台:{BuildTarget},获取AB后缀名失败!");
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取Asset打包信息文件名
        /// </summary>
        private string GetAssetBuildInfoAssetName()
        {
            if (BuildTarget == BuildTarget.StandaloneWindows || BuildTarget == BuildTarget.StandaloneWindows64)
            {
                return AssetBundlePath.WindowAssetBuildInfoAssetName;
            }
            if (BuildTarget == BuildTarget.Android)
            {
                return AssetBundlePath.AndroidAssetBuildInfoAssetName;
            }
            if (BuildTarget == BuildTarget.iOS)
            {
                return AssetBundlePath.IOSAssetBuildInfoAssetName;
            }
            else
            {
                Debug.LogError($"不支持的打包平台:{BuildTarget},获取Asset打包信息文件名失败!");
                return string.Empty;
            }
        }

        /// <summary>
        /// 检测资源是否有效
        /// </summary>
        private bool ValidateAsset(string assetPath)
		{
			if (!assetPath.StartsWith("Assets/"))
            {
				return false;
			}
			if (AssetDatabase.IsValidFolder(assetPath))
            {
				return false;
			}
			string ext = System.IO.Path.GetExtension(assetPath);
            if (AssetBundleCollectSettingData.Setting.BlackListInfo.IsBlackPostFix(ext))
            {
                return false;
            }
            string fileName = Path.GetFileName(assetPath);
            if (AssetBundleCollectSettingData.Setting.BlackListInfo.IsBlackFileName(fileName))
            {
                return false;
            }
			if(!AssetBundleCollectSettingData.IsCollectAsset(assetPath))
            {
				return false;
            }
			return true;
		}

		/// <summary>
		/// 设置资源的标签和变种
		/// </summary>
		private void SetAssetBundleLabelAndVariant(AssetInfo assetInfo)
		{
            // Note:
            // 不支持变体功能，变体功能只用于区分不同平台的AB包后缀
            string label = AssetBundleCollectSettingData.GetAssetBundleLabel(assetInfo.AssetPath);
            assetInfo.AssetBundleLabel = PathUtilities.GetRegularPath(label);
            assetInfo.AssetBundleVariant = GetBuildAssetBundlePostFix();
        }
        #endregion

        #region AssetBundle资源热更新相关
        /// <summary>
        /// 创建AssetBundle的MD5信息文件
        /// </summary>
        private void CreateAssetBundleMd5InfoFile()
        {
			var assetBundleMd5FilePath = AssetBundlePath.GetInnerAssetBundleMd5FilePath();
			// 确保创建最新的
			FileUtilities.DeleteFile(assetBundleMd5FilePath);
            VersionConfigModuleManager.Singleton.initVerisonConfigData();
            var resourceversion = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.ResourceVersionCode;
			// Note: 
			// 这里如果直接指定Encoding.UTF8会出现BOM文件(默认选择了带BOM的方式)
			// 最终导致热更新文件读取比较后的路径信息带了BOM导致https识别时报错导致下载不到正确的资源
            using (var md5SW = new StreamWriter(assetBundleMd5FilePath, false, new UTF8Encoding(false)))
            {
                var abFilesFullPath = Directory.GetFiles(OutputDirectory, "*.*", SearchOption.AllDirectories).Where(f =>
					!f.EndsWith(".meta") && !f.EndsWith(".manifest") && !f.EndsWith("readme.txt")
				);
                var md5hash = MD5.Create();
				// 格式:AB全路径+":"+MD5值
				foreach (var abFilePath in abFilesFullPath)
                {
					var abRelativePath = abFilePath.Remove(0, OutputDirectory.Length);
					abRelativePath = PathUtilities.GetRegularPath(abRelativePath);
					var fileMd5 = FileUtilities.GetFileMD5(abFilePath, md5hash);
					md5SW.WriteLine($"{abRelativePath}{ResourceConstData.AssetBundlleInfoSeparater}{fileMd5}");
                }
                Debug.Log($"AssetBundle的包内MD5信息计算完毕!");
            }
        }
        #endregion

        #region 视频相关
        private void PackVideo(List<AssetInfo> buildAssetInfoList)
		{
			// 注意：视频统一不压缩，避免播放有问题
			Log($"开始视频单独打包");
			for (int i = 0; i < buildAssetInfoList.Count; i++)
			{
				AssetInfo assetInfo = buildAssetInfoList[i];
				if (assetInfo.IsVideoAsset)
				{
					BuildAssetBundleOptions opt = BuildAssetBundleOptions.None;
					opt |= BuildAssetBundleOptions.DeterministicAssetBundle;
					opt |= BuildAssetBundleOptions.StrictMode;
					opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
					var videoObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(assetInfo.AssetPath);
					string outPath = OutputDirectory + "/" + assetInfo.AssetBundleLabel.ToLower();
					bool result = BuildPipeline.BuildAssetBundle(videoObj, new[] { videoObj }, outPath, opt, BuildTarget);
					if (result == false)
						throw new Exception($"视频单独打包失败：{assetInfo.AssetPath}");
				}
			}
		}
        #endregion

        #region 文件加密

        #endregion

        #region 文件相关

        /// <summary>
        /// 创建Asset AB打包详细Readme文件到输出目录
        /// </summary>
		/// <param name="assetBundleBuildList">Asset AB打包信息列表</param>
        private void CreateAssetBuildReadmeFile(List<AssetBundleBuild> assetBundleBuildList)
        {
            // 删除旧文件
            string filePath = $"{OutputDirectory}/{AssetBundleBuildConstData.AssetBuildReadmeFileName}";
            if (File.Exists(filePath))
                File.Delete(filePath);

            Log($"创建Asset AB打包详细说明文件：{filePath}");

            StringBuilder content = new StringBuilder();
            AppendData(content, $"构建平台：{BuildTarget}");
            AppendData(content, $"构建时间：{DateTime.Now}");

            AppendData(content, "");
            AppendData(content, $"--Asset AB打包信息--");
            for (int i = 0, length = assetBundleBuildList.Count; i < length; i++)
            {
                var assetBundleBuild = assetBundleBuildList[i];
				AppendData(content, $"AssetBundleName:{assetBundleBuild.assetBundleName} AssetBundleVariant:{assetBundleBuild.assetBundleVariant}");
				foreach(var assetPath in assetBundleBuild.assetNames)
                {
					AppendData(content, $"\tAssetPath: {assetPath}");
                }
                AppendData(content, "");
            }
            // 创建新文件
            File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 1. 检测循环依赖
        /// </summary>
        private void CheckCycleDepend(AssetBundleManifest unityManifest)
		{
			List<string> visited = new List<string>(100);
			List<string> stack = new List<string>(100);
			string[] allAssetBundles = unityManifest.GetAllAssetBundles();
			for (int i = 0; i < allAssetBundles.Length; i++)
			{
				var element = allAssetBundles[i];
				visited.Clear();
				stack.Clear();

				// 深度优先搜索检测有向图有无环路算法
				if (CheckCycle(unityManifest, element, visited, stack))
				{
					foreach (var ele in stack)
					{
						UnityEngine.Debug.LogWarning(ele);
					}
					throw new Exception($"Found cycle assetbundle : {element}");
				}
			}
		}

        /// <summary>
        /// 检查循环依赖
        /// </summary>
        /// <param name="unityManifest"></param>
        /// <param name="element"></param>
        /// <param name="visited"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
		private bool CheckCycle(AssetBundleManifest unityManifest, string element, List<string> visited, List<string> stack)
		{
			if (visited.Contains(element) == false)
			{
				visited.Add(element);
				stack.Add(element);

				string[] depends = unityManifest.GetDirectDependencies(element);
				foreach (var dp in depends)
				{
					if (visited.Contains(dp) == false && CheckCycle(unityManifest, dp, visited, stack))
						return true;
					else if (stack.Contains(dp))
						return true;
				}
			}

			stack.Remove(element);
			return false;
		}

		/// <summary>
		/// 3. 创建Readme文件到输出目录
		/// </summary>
		private void CreateReadmeFile(AssetBundleManifest unityManifest)
		{
			string[] allAssetBundles = unityManifest.GetAllAssetBundles();

			// 删除旧文件
			string filePath = $"{OutputDirectory}/{AssetBundleBuildConstData.ReadmeFileName}";
			if (File.Exists(filePath))
				File.Delete(filePath);

			Log($"创建说明文件：{filePath}");

			StringBuilder content = new StringBuilder();
			AppendData(content, $"构建平台：{BuildTarget}");
			AppendData(content, $"构建时间：{DateTime.Now}");

			AppendData(content, "");
			AppendData(content, $"--配置信息--");
			for (int i = 0; i < AssetBundleCollectSettingData.Setting.AssetBundleCollectors.Count; i++)
			{
				Collector wrapper = AssetBundleCollectSettingData.Setting.AssetBundleCollectors[i];
                if(wrapper.BuildRule != EAssetBundleBuildRule.ByConstName)
                {
                    AppendData(content, $"Directory : {wrapper.CollectFolderPath} || CollectRule : {wrapper.CollectRule} || BuildRule : {wrapper.BuildRule}");
                }
                else
                {
                    AppendData(content, $"Directory : {wrapper.CollectFolderPath} || CollectRule : {wrapper.CollectRule} || BuildRule : {wrapper.BuildRule} || ConstName : {wrapper.ConstName}");
                }
            }

			AppendData(content, "");
			AppendData(content, $"--构建参数--");
			AppendData(content, $"CompressOption：{CompressOption}");
			AppendData(content, $"ForceRebuild：{IsForceRebuild}");
			AppendData(content, $"DisableWriteTypeTree：{IsDisableWriteTypeTree}");
			AppendData(content, $"IgnoreTypeTreeChanges：{IsIgnoreTypeTreeChanges}");

			AppendData(content, "");
			AppendData(content, $"--构建清单--");
			for (int i = 0; i < allAssetBundles.Length; i++)
			{
				AppendData(content, allAssetBundles[i]);
			}
            
			// 创建新文件
			File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
		}

		private void AppendData(StringBuilder sb, string data)
		{
			sb.Append(data);
			sb.Append("\r\n");
		}
        #endregion
    }
}