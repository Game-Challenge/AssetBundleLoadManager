﻿/*
 * Description:             AssetBundleLoader.cs
 * Author:                  TONYTANG
 * Create Date:             2021//10/13
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TResource
{
    /// <summary>
    /// AssetBundleLoader.cs
    /// AssetBundle加载器
    /// </summary>
    public class AssetBundleLoader : Loadable
    {
        /// <summary>
        /// 加载任务对应的AB资源路径
        /// </summary>
        public string AssetBundlePath
        {
            get;
            set;
        }

        /// <summary>
        /// AssetBundle
        /// </summary>
        private AssetBundle mBundle;

        /// <summary>
        /// 所有AB资源加载完成逻辑层回调
        /// </summary>
        protected Action<AssetBundle> mLoadABCompleteCallBack;

        /// <summary>
        /// 所有AB资源加载完成逻辑回调Map<请求UID,逻辑回调>
        /// </summary>
        protected Dictionary<int, Action<AssetBundle>> mLoadABCompleteCallBackMap;

        /// <summary>
        /// 资源加载方式
        /// </summary>
        public ResourceLoadMethod LoadMethod
        {
            get;
            set;
        }

        /// <summary>
        /// 资源加载类型
        /// </summary>
        public ResourceLoadType LoadType
        {
            get;
            set;
        }

        /// <summary> AB资源自身加载任务状态 /// </summary>
        public ResourceLoadState LoadState
        {
            get;
            set;
        }

        /// <summary>
        /// 是否加载完成
        /// </summary>
        public bool IsDone
        {
            get
            {
                return LoadState == ResourceLoadState.Complete || LoadState == ResourceLoadState.Error || LoadState == ResourceLoadState.Cancel;
            }
        }

        /// <summary>
        /// 是否在等待
        /// </summary>
        public bool IsWaiting
        {
            get
            {
                return LoadState == ResourceLoadState.Waiting;
            }
        }

        /// <summary>
        /// 是否在加载中
        /// </summary>
        public bool IsLoading
        {
            get
            {
                return LoadState == ResourceLoadState.Loading;
            }
        }

        /// <summary>
        /// 是否加载失败
        /// </summary>
        public bool IsError
        {
            get
            {
                return LoadState == ResourceLoadState.Error;
            }
        }

        /// <summary>
        /// 加载打断
        /// </summary>
        protected void cancel()
        {
            if (IsDone)
            {
                Debug.LogError($"AssetBundle:{AssetBundlePath}已加载完成不允许取消!");
                return;
            }
            LoadState = ResourceLoadState.Cancel;
            onCancel();
        }

        protected void onCancel()
        {
            ResourceLogger.log($"AssetBundle:{AssetBundlePath}加载请求取消!");
            // TODO: 处理还在加载或者在队列中的情况
            complete();
        }


        /// <summary>
        /// 完成加载
        /// </summary>
        private void complete()
        {
            ResourceLogger.log($"加载AB:{AssetBundlePath}完成!");
            LoadState = ResourceLoadState.Complete;
            onComplete();
        }

        /// <summary>
        /// 响应加载完成
        /// </summary>
        private void onComplete()
        {
            // 通知上层ab加载完成
            mLoadABCompleteCallBack(mBundle);
            mLoadABCompleteCallBack = null;

            // AB加载完成后，AssetBundleLoader的任务就完成了，回收重用
            ObjectPool.Singleton.push<AssetBundleLoader>(this);
        }


        /// <summary>
        /// 添加AB加载完成逻辑回调
        /// </summary>
        /// <param name="requestUID"></param>
        /// <param name="loadABCompleteCallBack"></param>
        /// <returns></returns>
        public bool addLoadABCompleteCallBack(int requestUID, Action<AssetBundle> loadABCompleteCallBack)
        {
            if (!mLoadABCompleteCallBackMap.ContainsKey(requestUID))
            {
                ResourceLogger.log($"绑定AssetBundle:{AssetBundlePath}加载请求UID:{requestUID}成功!");
                mLoadABCompleteCallBackMap.Add(requestUID, loadABCompleteCallBack);
                if (loadABCompleteCallBack != null)
                {
                    mLoadABCompleteCallBack += loadABCompleteCallBack;
                }
                LoaderManager.Singleton.addAssetBundleRequestUID(requestUID, AssetBundlePath);
                return true;
            }
            else
            {
                Debug.LogError($"重复绑定相同请求UID:{requestUID}回调,绑定AssetBundle:{AssetBundlePath}请求回调失败!");
                return false;
            }
        }

        /// <summary>
        /// 取消指定请求UID请求
        /// </summary>
        /// <param name="requestUID"></param>
        /// <returns></returns>
        public bool cancelRequest(int requestUID)
        {
            Action<AssetBundle> loadABCompleteCallBack;
            if (mLoadABCompleteCallBackMap.TryGetValue(requestUID, out loadABCompleteCallBack))
            {
                ResourceLogger.log($"AssetBundle:{AssetBundlePath}取消请求UID:{requestUID}成功!");
                removeRequest(requestUID);
                if (loadABCompleteCallBack != null)
                {
                    mLoadABCompleteCallBack -= loadABCompleteCallBack;
                }
                // 所有请求都取消表示没人再请求此AB了
                if (mLoadABCompleteCallBackMap.Count == 0)
                {
                    cancel();
                }
                return true;
            }
            else
            {
                Debug.LogError($"找不到请求UID:{requestUID}请求,取消AssetBundle:{AssetBundlePath}请求失败!");
                return false;
            }
        }

        /// <summary>
        /// 清除指定UID请求
        /// </summary>
        /// <param name="requestUID"></param>
        /// <returns></returns>
        private bool removeRequest(int requestUID)
        {
            Action<AssetBundle> loadABCompleteCallBack;
            if (mLoadABCompleteCallBackMap.TryGetValue(requestUID, out loadABCompleteCallBack))
            {
                mLoadABCompleteCallBackMap.Remove(requestUID);
                LoaderManager.Singleton.removeAssetBundleRequestUID(requestUID);
                return true;
            }
            else
            {
                Debug.LogError($"找不到请求UID:{requestUID}回调,移除AssetBundle:{AssetBundlePath}请求失败!");
                return false;
            }
        }

    }
}