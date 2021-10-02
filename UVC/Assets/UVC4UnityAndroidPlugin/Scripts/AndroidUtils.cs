using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

#if UNITY_ANDROID
#if UNITY_2018_3_OR_NEWER
using UnityEngine.Android;
#endif
#endif

namespace Serenegiant
{

	public class AndroidUtils : MonoBehaviour
	{
		public const string FQCN_UNITY_PLAYER = "com.unity3d.player.UnityPlayer";
		public const string PERMISSION_CAMERA = "android.permission.CAMERA";

		public enum PermissionGrantResult {
			PERMISSION_GRANT = 0,
			PERMISSION_DENY = -1,
			PERMISSION_DENY_AND_NEVER_ASK_AGAIN = -2
		}

		private const string TAG = "AndroidUtils#";
		private const string FQCN_PLUGIN = "com.serenegiant.androidutils.AndroidUtils";

        //--------------------------------------------------------------------------------
        /**
		 * 生命周期事件用定界符
		 * @param resumed true: onResume, false: onPause
		 */
        public delegate void LifecycleEventHandler(bool resumed);

        /***
		 * 在GrantPermission要求权限时的回呼用delegateer
		 * @param permission
		 * @param grantResult 0:grant, -1:deny, -2:denyAndNeverAskAgain
		*/
        public delegate void OnPermission(string permission, PermissionGrantResult result);

        //--------------------------------------------------------------------------------
        /**
		 * 请求权限时的超时
		 */
        public static float PermissionTimeoutSecs = 30;
	
		public event LifecycleEventHandler LifecycleEvent;

		public static bool isPermissionRequesting;
		private static PermissionGrantResult grantResult;

		void Awake()
		{
            AndroidDebug.Logd(TAG, "Awake:");
#if UNITY_ANDROID
			Input.backButtonLeavesApp = true;   // 使用终端的后按键关闭应用程序
            Initialize();
#endif
		}

		//--------------------------------------------------------------------------------
		// Java側からのイベントコールバック

		/**
		 * onStart事件
		 */
		public void OnStartEvent()
		{
            AndroidDebug.Logd(TAG, "OnStartEvent:");
        }

		/**
		 * onResumeイベント
		 */
		public void OnResumeEvent()
		{
            Debug.Log(TAG + "OnResumeEvent:");
            LifecycleEvent?.Invoke(true);
		}

		/**
		 * onPauseイベント
		 */
		public void OnPauseEvent()
		{
            Debug.Log(TAG + "OnPauseEvent:");
            LifecycleEvent?.Invoke(false);
		}

		/**
		 * onStopイベント
		 */
		public void OnStopEvent()
		{
            Debug.Log(TAG + "OnStopEvent:");
        }

		/**
		 * パーミッションを取得できた
		 */
		public void OnPermissionGrant()
		{
            Debug.Log(TAG + "OnPermissionGrant:");
            grantResult = PermissionGrantResult.PERMISSION_GRANT;
			isPermissionRequesting = false;
		}

		/**
		 * パーミッションを取得できなかった
		 */
		public void OnPermissionDeny()
		{
            Debug.Log(TAG + "OnPermissionDeny:");
            grantResult = PermissionGrantResult.PERMISSION_DENY;
			isPermissionRequesting = false;
		}

		/**
		 * パーミッションを取得できずパーミッションダイアログを再び表示しないように設定された
		 */
		public void OnPermissionDenyAndNeverAskAgain()
		{
            Debug.Log(TAG + "OnPermissionDenyAndNeverAskAgain:");
            grantResult = PermissionGrantResult.PERMISSION_DENY_AND_NEVER_ASK_AGAIN;
			isPermissionRequesting = false;
		}

        //--------------------------------------------------------------------------------
#if UNITY_ANDROID
        /**
		 * 初始化插件
		 */
        private void Initialize()
		{
			using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
			{
				clazz.CallStatic("initialize",
					AndroidUtils.GetCurrentActivity(), gameObject.name);
                Debug.Log(TAG + "初始化插件传入参数：AndroidUtils.GetCurrentActivity()" + AndroidUtils.GetCurrentActivity() + "," + gameObject.name);
			}
		}

        /**
		 * 获得是否保持指定权限
		 * @param permission
		 * @param 指定したパーミッションを保持している
		 */
        public static bool HasPermission(string permission)
		{
			using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
			{
                Debug.Log(TAG + "OnPermissionDenyAndNeverAskAgain:");
                return clazz.CallStatic<bool>("hasPermission",
					AndroidUtils.GetCurrentActivity(), permission);
			}
		}

        /**
		 * 获得是否需要显示指定权限的说明
		 * @param permission
		 * @param 指定したパーミッションの説明を表示する必要がある
		 */
        public static bool ShouldShowRequestPermissionRationale(string permission)
		{
			using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
			{
                Debug.Log(TAG + "ShouldShowRequestPermissionRationale:");
                return clazz.CallStatic<bool>("shouldShowRequestPermissionRationale",
					AndroidUtils.GetCurrentActivity(), permission);
			}
		}

        /**
		 *权限要求
         *此处不在Java侧进行Rationale的处理等
		 * @param permission
		 * @param callback
		 */
        public static IEnumerator RequestPermission(string permission, OnPermission callback)
		{
            Debug.Log(TAG + "RequestPermission:"+ permission);
            if (!HasPermission(permission))
			{
				grantResult = PermissionGrantResult.PERMISSION_DENY;
				isPermissionRequesting = true;
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					clazz.CallStatic("requestPermission",
						AndroidUtils.GetCurrentActivity(), permission);
				}
				float timeElapsed = 0;
				while (isPermissionRequesting)
				{
					if ((PermissionTimeoutSecs > 0) && (timeElapsed > PermissionTimeoutSecs))
					{
						isPermissionRequesting = false;
						yield break;
					}
					timeElapsed += Time.deltaTime;
					yield return null;
				}
				callback(permission, grantResult);
			}
			else
			{
				callback(permission, PermissionGrantResult.PERMISSION_GRANT);
			}
	
			yield break;
		}

        /**
		 *权限要求
         *在Java方面进行Rationale的处理等
		 * @param permission
		 * @param callback
		 */
        public static IEnumerator GrantPermission(string permission, OnPermission callback)
		{
            Debug.Log(TAG + "GrantPermission:" + permission);
            if (!HasPermission(permission))
			{
				grantResult = PermissionGrantResult.PERMISSION_DENY;
				isPermissionRequesting = true;
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					clazz.CallStatic("grantPermission",
						AndroidUtils.GetCurrentActivity(), permission);
				}
				float timeElapsed = 0;
				while (isPermissionRequesting)
				{
					if ((PermissionTimeoutSecs > 0) && (timeElapsed > PermissionTimeoutSecs))
					{
						isPermissionRequesting = false;
						yield break;
					}
					timeElapsed += Time.deltaTime;
						yield return null;
				}
				callback(permission, grantResult);
			}
			else
			{
				callback(permission, PermissionGrantResult.PERMISSION_GRANT);
			}
	
			yield break;
		}

        /**
		 * 请求照相机权限
		 * @param callback
		 */
        public static IEnumerator GrantCameraPermission(OnPermission callback)
		{
            Debug.Log(TAG + "GrantCameraPermission:");
            if (CheckAndroidVersion(23))
			{
                // Android 9以后，访问UVC设备也需要CAMERA权限
                yield return GrantPermission(PERMISSION_CAMERA, callback);
			}
			else
			{
                // 不满Android 6的话不需要权限要求处理
                callback(PERMISSION_CAMERA, PermissionGrantResult.PERMISSION_GRANT);
			}

			yield break;
		}

        /**
		 * 获得Unity PlayerActivity
		 */
        public static AndroidJavaObject GetCurrentActivity()
		{
			using (AndroidJavaClass playerClass = new AndroidJavaClass(FQCN_UNITY_PLAYER))
			{
				return playerClass.GetStatic<AndroidJavaObject>("currentActivity");
			}
		}

        /**
		 *确认是否在指定版本之后
         *@param api Level
         *@return真：在指定版本之后运行的假终端：在比指定版本更旧的终端上运行
		 */
        public static bool CheckAndroidVersion(int apiLevel)
		{
			using (var VERSION = new AndroidJavaClass("android.os.Build$VERSION"))
			{
				return VERSION.GetStatic<int>("SDK_INT") >= apiLevel;
			}
		}

	} // class AndroidUtils

} // namespace Serenegiant

#endif // #if UNITY_ANDROID