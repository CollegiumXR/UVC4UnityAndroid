using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_ANDROID && UNITY_2018_3_OR_NEWER
using UnityEngine.Android;
#endif

namespace Serenegiant.UVC
{

	[RequireComponent(typeof(AndroidUtils))]
	public class UVCManager : MonoBehaviour
	{
		private const string TAG = "UVCManager#";
		private const string FQCN_PLUGIN = "com.serenegiant.uvcplugin.DeviceDetector";
		private const string FQCN_UVC = "com.serenegiant.usb.uvc.IUVCCameraControl";

        /**
		 *未设置IUVC Selector时
         *或IUVC Selector在选择分辨率时选择null
         *返回时的默认分辨率（宽度）
		 */
        public int DefaultWidth = 1280;
        /**
		 *未设置IUVC Selector时
         *或IUVC Selector在选择分辨率时选择null
         *返回时的默认分辨率（高度）
		 */
        public int DefaultHeight = 720;
        /**
		 *与UVC机器协商时
         *H.264是否优先协商
         *仅Android实机有效
         *true:H.264 MJPEG＞YUV
         *假：MJPEG>H.264＞YUV
		 */
        public bool PreferH264 = false;
        /**
		 * 与UVC相关的以本德处理程序
		 */
        [SerializeField, ComponentRestriction(typeof(IUVCDrawer))]
		public Component[] UVCDrawers;

        /**
		 * 用于获取插件的渲染事件的native（c/c++）函数
		 */
        [DllImport("uvc-plugin")]
		private static extern IntPtr GetRenderEventFunc();

		public class CameraInfo
		{
			internal readonly UVCDevice device;
            /**
			 * 预览中的UVC相机标识符，用于渲染事件
			 */
            internal Int32 activeCameraId;
			internal Texture previewTexture;

			internal CameraInfo(UVCDevice device)
			{
				this.device = device;
			}

            /**
			 * 获取设备名称
			 */
            public string DeviceName
			{
				get { return device.deviceName;  }
			}

            /**
			 * 获取供应商ID
			 */
            public int Vid
			{
				get { return device.vid;  }
			}

            /**
			 * 获取产品ID
			 */
            public int Pid
			{
				get { return device.pid;  }
			}

            /**
			 *是否开启相机
             *是否正在获取影像请使用IsPreviewing
			 */
            public bool IsOpen
			{
				get { return (activeCameraId != 0); }
			}

            /**
			 *是否正在获取影像
			 */
            public bool IsPreviewing
			{
				get { return IsOpen && (previewTexture != null); }
			}

            /**
			 *当前分辨率（宽度）
             *如果不是预览中，则0
			 */
            public int CurrentWidth
			{
				get { return currentWidth;  }
			}

            /**
			 *当前分辨率（高度）
             *如果不是预览中，则0
			 */
            public int CurrentHeight
			{
				get { return currentHeight; }
			}

			private int currentWidth;
			private int currentHeight;
            /**
			 * 更改当前分辨率
			 * @param width
			 * @param height
			 */
            internal void SetSize(int width, int height)
			{
				currentWidth = width;
				currentHeight = height;
			}

            /**
			 *渲染器事件处理用
             *作为编码程序执行
			 */
            internal IEnumerator OnRender()
			{
				var renderEventFunc = GetRenderEventFunc();
				for (; activeCameraId != 0; )
				{
					yield return new WaitForEndOfFrame();
					GL.IssuePluginEvent(renderEventFunc, activeCameraId);
				}
				yield break;
			}
		}

        /**
		 *保持正在处理的相机信息
         *保持string（deviceName）-CamelInfo对
		 */
        private Dictionary<string, CameraInfo> cameraInfos = new Dictionary<string, CameraInfo>();

        //================================================================================
        // 来自Unity Engine的呼叫

        IEnumerator Start()
		{
            AndroidDebug.Logd(TAG, "Start:");
            yield return Initialize();
		}

#if (!NDEBUG && DEBUG && ENABLE_LOG)
		void OnApplicationFocus()
		{
           AndroidDebug.Logd(TAG, "OnApplicationFocus:");
		}
#endif

#if (!NDEBUG && DEBUG && ENABLE_LOG)
		void OnApplicationPause(bool pauseStatus)
		{
			AndroidDebug.Logd(TAG, "OnApplicationPause:"+pauseStatus);
		}
#endif

#if (!NDEBUG && DEBUG && ENABLE_LOG)
		void OnApplicationQuits()
		{
			AndroidDebug.Logd(TAG, "OnApplicationQuits:");
		}
#endif

        void OnDestroy()
		{
#if (!NDEBUG && DEBUG && ENABLE_LOG)
			AndroidDebug.Logd(TAG, "OnDestroy:");
#endif
            CloseAll();
		}

        //================================================================================
        /**
		 *获取连接中的UVC设备一览
         *@return连接中的UVC设备列表List
		 */
        public List<CameraInfo> GetAttachedDevices()
		{
			var result = new List<CameraInfo>(cameraInfos.Count);

			foreach (var info in cameraInfos.Values)
			{
				result.Add(info);
                AndroidDebug.Logd(TAG, "连接的设备信息:"+ info);
            }
	
			return result;
		}

        /**
		 *收获对应分辨率
         *@param camera指定获得对应分辨率的UVC设备
         *@return对应分辨率如果照相机已经被取下了/close，则null
		 */
        public SupportedFormats GetSupportedVideoSize(CameraInfo camera)
		{
			var info = (camera != null) ? Get(camera.DeviceName) : null;
			if ((info != null) && info.IsOpen)
			{
                AndroidDebug.Logd(TAG, "收获对应分辨率:" + info.DeviceName);
                return GetSupportedVideoSize(info.DeviceName);
			} else
			{
                AndroidDebug.Logd(TAG, "收获对应分辨率为空:");
                return null;
			}
		}

        /**
		 *更改分辨率
         *@param指定更改分辨率的UVC设备
         *@param指定要变更的分辨率，null则返回默认值
         *@param分辨率是否变更
		 */
        public bool SetVideoSize(CameraInfo camera, SupportedFormats.Size size)
		{
            AndroidDebug.Logd(TAG, "更改分辨率:");
            var info = (camera != null) ? Get(camera.DeviceName) : null;
			var width = size != null ? size.Width : DefaultWidth;
			var height = size != null ? size.Height : DefaultHeight;
			if ((info != null) && info.IsPreviewing)
			{
				if ((width != info.CurrentWidth) || (height != info.CurrentHeight))
                {   // 当分辨率改变时
                    StopPreview(info.DeviceName);
					StartPreview(info.DeviceName, width, height);
					return true;
				}
			}
			return false;
		}

        //================================================================================
        //Android固有处理
        //Java方面的事件回呼

        /**
		 *UVC设备已连接
         *@param args UVC设备识别字符串
		 */
        void OnEventAttach(string args)
		{
            AndroidDebug.Logd(TAG, "UVC设备已连接:"+ args);
            if (!String.IsNullOrEmpty(args))
			{   // argsはdeviceName
				var info = CreateIfNotExist(args);
				if (HandleOnAttachEvent(info))
				{
					RequestUsbPermission(args);
				} else
				{
					Remove(args);
				}
			}
            AndroidDebug.Logd(TAG, "UVC设备已连接完成:" + args);
        }

        /**
		 *UVC机器已拆下
         *@param args UVC设备识别字符串
		 */
        void OnEventDetach(string args)
		{
            AndroidDebug.Logd(TAG, "UVC机器已拆下:" + args);
            var info = Get(args);
			if (info != null)
			{
				HandleOnDetachEvent(info);
				Close(args);
				Remove(args);
			}
		}

        /**
		 *获得了访问UVC设备的权限
         *@param args UVC设备的识别字符串
		 */
        void OnEventPermission(string args)
		{
            AndroidDebug.Logd(TAG, "获得了访问UVC设备的权限:" + args);
            if (!String.IsNullOrEmpty(args))
			{   // argsはdeviceName
				Open(args);
			}
		}

        /**
		 *打开了UVC机器
         *@param args UVC设备的识别字符串
		 */
        void OnEventConnect(string args)
		{
            AndroidDebug.Logd(TAG, "打开了UVC机器:" + args);
        }

        /**
		 *UVC设备关闭
         *@param args UVC设备的识别字符串
		 */
        void OnEventDisconnect(string args)
		{
            AndroidDebug.Logd(TAG, "UVC设备关闭:" + args);
            //该事件在Unity方面发出close请求时以外也会发生
            //为了慎重起见，事先叫上Close
            Close(args);
		}

        /**
         *可以接受影像了
         *@param args UVC设备的识别字符串
        */
        void OnEventReady(string args)
		{
            AndroidDebug.Logd(TAG, "可以接受影像了:" + args);
            StartPreview(args);
		}

        /**
		 * 开始从UVC设备获取影像
		 * @param args UVC设备的识别字符串
		 */
        void OnStartPreview(string args)
		{
            AndroidDebug.Logd(TAG, "开始从UVC设备获取影像:"+ args);
            var info = Get(args);
			if ((info != null) && info.IsPreviewing && (UVCDrawers != null))
			{
				foreach (var drawer in UVCDrawers)
				{
					if ((drawer is IUVCDrawer) && (drawer as IUVCDrawer).CanDraw(this, info.device))
					{
						(drawer as IUVCDrawer).OnUVCStartEvent(this, info.device, info.previewTexture);
					}
				}
			}
		}

        /**
		 * 结束了从UVC设备获取影像
		 * @param args UVC设备的识别字符串
		 */
        void OnStopPreview(string args)
		{
            AndroidDebug.Logd(TAG, "结束了从UVC设备获取影像:" + args);
            var info = Get(args);
			if ((info != null) && info.IsPreviewing && (UVCDrawers != null))
			{
				info.SetSize(0, 0);
				foreach (var drawer in UVCDrawers)
				{
					if ((drawer is IUVCDrawer) && (drawer as IUVCDrawer).CanDraw(this, info.device))
					{
						(drawer as IUVCDrawer).OnUVCStopEvent(this, info.device);
					}
				}
			}
		}

        /**
		 *收到UVC设备的状态事件
         *@param args UVC设备识别字符串+状态
		 */
        void OnReceiveStatus(string args)
		{
            AndroidDebug.Logd(TAG, "收到UVC设备的状态事件:" + args);
            // 未实现FIXME
        }

        /**
		 *收到了UVC设备的按钮事件
         *@param args UVC设备识别字符串+按钮事件
		 */
        void OnButtonEvent(string args)
		{
            AndroidDebug.Logd(TAG, "收到了UVC设备的按钮事件:" + args);
            // 未实现FIXME
        }

        /**
		 * onResume活动
		 */
        IEnumerator OnResumeEvent()
		{
            AndroidDebug.Logd(TAG, "onResume活动1:"+ AndroidUtils.isPermissionRequesting+"-"+ AndroidUtils.CheckAndroidVersion(28));
            AndroidDebug.Logd(TAG, "onResume活动2:" + AndroidUtils.CheckAndroidVersion(28));
            AndroidDebug.Logd(TAG, "onResume活动3:" + "-" + AndroidUtils.HasPermission(AndroidUtils.PERMISSION_CAMERA));
            if (!AndroidUtils.isPermissionRequesting
				&& AndroidUtils.CheckAndroidVersion(28)
				&& !AndroidUtils.HasPermission(AndroidUtils.PERMISSION_CAMERA))
			{
                AndroidDebug.Logd(TAG, "onResume活动:" + AndroidUtils.isPermissionRequesting);
                yield return Initialize();
			}
            AndroidDebug.Logd(TAG, "onResume活动3:" + "-" + cameraInfos.Count);

            KeyValuePair<string, CameraInfo>? found = null;
			foreach (var elm in cameraInfos)
			{
				if (elm.Value.activeCameraId == 0)
                {   //有附接但未打开的设备时
                    found = elm;
					break;
				}
			}
			if (found != null)
            {   //有附接但未打开的设备时
                var deviceName = found?.Key;
				if (!AndroidUtils.isPermissionRequesting)
                { //不在请求权限时
                    RequestUsbPermission(deviceName);
				}
				else if (HasUsbPermission(deviceName))
                { //已经有权限时
                    AndroidUtils.isPermissionRequesting = false;
					OnEventPermission(deviceName);
				}
			}

			yield break;
		}

        /**
		 * onPause活动
		 */
        void OnPauseEvent()
		{
            AndroidDebug.Logd(TAG, "OnPauseEvent:");
            CloseAll();
		}

		//--------------------------------------------------------------------------------
		private IEnumerator Initialize()
		{
            AndroidDebug.Logd(TAG, "Initialize:");
            if (AndroidUtils.CheckAndroidVersion(28))
			{
				yield return AndroidUtils.GrantCameraPermission((string permission, AndroidUtils.PermissionGrantResult result) =>
				{
                    AndroidDebug.Logd(TAG, "Initialize:"+ result+"-"+ permission);
                    switch (result)
					{
						case AndroidUtils.PermissionGrantResult.PERMISSION_GRANT:
							InitPlugin();
							break;
						case AndroidUtils.PermissionGrantResult.PERMISSION_DENY:
							if (AndroidUtils.ShouldShowRequestPermissionRationale(AndroidUtils.PERMISSION_CAMERA))
							{
                                //未能取得权限
                                //必须显示FIXME说明用对话框等
                            }
                            break;
						case AndroidUtils.PermissionGrantResult.PERMISSION_DENY_AND_NEVER_ASK_AGAIN:
							break;
					}
				});
			} else
			{
				InitPlugin();
			}

			yield break;
		}

        //对Uvc-Plugin-unity的处理要求

        /**
         *初始化插件
        */
        private void InitPlugin()
		{
            AndroidDebug.Logd(TAG, "InitPlugin:");
            //确认是否分配了IUVC Drawers
            var hasDrawer = false;
			if ((UVCDrawers != null) && (UVCDrawers.Length > 0))
			{
				foreach (var drawer in UVCDrawers)
				{
					if (drawer is IUVCDrawer)
					{
						hasDrawer = true;
						break;
					}
				}
			}
			if (!hasDrawer)
			{  // 在检查器中未设定IUVCD服务器时
               //试图从已加载了该脚本的游戏对象中获取
                AndroidDebug.Logd(TAG, "InitPlugin:has no IUVCDrawer, try to get from gameObject");
                var drawers = GetComponents(typeof(IUVCDrawer));
				if ((drawers != null) && (drawers.Length > 0))
				{
					UVCDrawers = new Component[drawers.Length];
					int i = 0;
					foreach (var drawer in drawers)
					{
						UVCDrawers[i++] = drawer;
					}
				}
			}

			using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
			{
				clazz.CallStatic("initDeviceDetector",
					AndroidUtils.GetCurrentActivity(), gameObject.name);
			}
		}

        /**
		 *获取是否具有访问指定USB设备的权限
         *@param deviceName UVC设备识别字符串
		 */
        private bool HasUsbPermission(string deviceName)
		{
			if (!String.IsNullOrEmpty(deviceName))
			{
                AndroidDebug.Logd(TAG, "HasUsbPermission:");
                using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					return clazz.CallStatic<bool>("hasPermission",
						AndroidUtils.GetCurrentActivity(), deviceName);
				}
			}
			else
			{
				return false;
			}
		}

        /**
		 * USB设备访问权限要求
		 * @param deviceName UVC设备识别字符串
		 */
        private void RequestUsbPermission(string deviceName)
		{
            AndroidDebug.Logd(TAG, "USB设备访问权限要求:" + deviceName);
            if (!String.IsNullOrEmpty(deviceName))
			{
				AndroidUtils.isPermissionRequesting = true;

				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					clazz.CallStatic("requestPermission",
						AndroidUtils.GetCurrentActivity(), deviceName);
				}
			}
			else
			{
				throw new ArgumentException("device name is empty/null");
			}
#if (!NDEBUG && DEBUG && ENABLE_LOG)
			Console.WriteLine($"{TAG}RequestUsbPermission[{Time.frameCount}]:finsihed");
#endif
		}

        /**
		 * 启动指定的UVC机器
		 * @param deviceName UVC设备识别字符串
		 */
        private void Open(string deviceName)
		{
            AndroidDebug.Logd(TAG, "USB设备访问权限要求:" + deviceName);
            var info = Get(deviceName);
			if (info != null)
			{
				AndroidUtils.isPermissionRequesting = false;
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					info.activeCameraId = clazz.CallStatic<Int32>("openDevice",
						AndroidUtils.GetCurrentActivity(), deviceName,
						DefaultWidth, DefaultHeight, PreferH264);
				}
			}
			else
			{
				throw new ArgumentException("device name is empty/null");
			}
		}

        /**
		 * 对指定的UVC设备进行close
		 * @param deviceName UVC设备识别字符串
		 */
        private void Close(string deviceName)
		{
            AndroidDebug.Logd(TAG, "对指定的UVC设备进行close:" + deviceName);
            var info = Get(deviceName);
			if ((info != null) && (info.activeCameraId != 0))
			{
				info.SetSize(0, 0);
				info.activeCameraId = 0;
				info.previewTexture = null;
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					clazz.CallStatic("closeDevice",
						AndroidUtils.GetCurrentActivity(), deviceName);
				}
			}
#if (!NDEBUG && DEBUG && ENABLE_LOG)
			Console.WriteLine($"{TAG}Close:finished");
#endif
		}

        /**
		 * 关闭所有Open的UVC设备
		 */
        private void CloseAll()
		{
            AndroidDebug.Logd(TAG, "关闭所有Open的UVC设备:");
            List<string> keys = new List<string>(cameraInfos.Keys);
			foreach (var deviceName in keys)
			{
				Close(deviceName);
			}
		}

        /**
		 * 请求开始接受UVC机器的影像
		 * @param deviceName UVC機器識別文字列
		 */
        private void StartPreview(string deviceName)
		{
            AndroidDebug.Logd(TAG, "请求开始接受UVC机器的影像:" + deviceName);
            var info = Get(deviceName);
			if ((info != null) && (info.activeCameraId != 0))
			{
				int width = DefaultWidth;
				int height = DefaultHeight;

				var supportedVideoSize = GetSupportedVideoSize(deviceName);
				if (supportedVideoSize == null)
				{
					throw new ArgumentException("fauled to get supported video size");
				}

				// 解像度の選択処理
				if ((UVCDrawers != null) && (UVCDrawers.Length > 0))
				{
					foreach (var drawer in UVCDrawers)
					{
						if ((drawer is IUVCDrawer) && ((drawer as IUVCDrawer).CanDraw(this, info.device)))
						{
							var size = (drawer as IUVCDrawer).OnUVCSelectSize(this, info.device, supportedVideoSize);
                            AndroidDebug.Logd(TAG, "请求开始接受UVC机器的影像:" + deviceName+"-"+ size);
                            if (size != null)
							{   // 一番最初に見つかった描画可能なIUVCDrawersがnull以外を返せばそれを使う
								width = size.Width;
								height = size.Height;
								break;
							}
						}
					}
				}

				StartPreview(deviceName, width, height);
			}
		}

		/**
		*请求开始接受UVC机器的影像
        *通常通过StartPreview（string deviceName）调用
		 * @param deviceName UVC機器識別文字列
		 * @param width
		 * @param height
		 */
		private void StartPreview(string deviceName, int width, int height)
		{
            AndroidDebug.Logd(TAG, "请求开始接受UVC机器的影像:" + deviceName + "-" + width + "-" + height);
            var info = Get(deviceName);
			if (info != null)
            {  //连接时
                var supportedVideoSize = GetSupportedVideoSize(deviceName);
				if (supportedVideoSize == null)
				{
                    AndroidDebug.Logd(TAG, "连接时:fauled to get supported video size");
                    throw new ArgumentException("fauled to get supported video size");
				}
                //对应分辨率的检查
                if (supportedVideoSize.Find(width, height/*,minFps=0.1f, maxFps=121.0f*/) == null)
                {   //不支持指定的分辨率
#if (!NDEBUG && DEBUG && ENABLE_LOG)
					Console.WriteLine($"{TAG}StartPreview:{width}x{height} is NOT supported.");
					Console.WriteLine($"{TAG}Info={GetDevice(deviceName)}");
					Console.WriteLine($"{TAG}supportedVideoSize={supportedVideoSize}");
#endif
                    AndroidDebug.Logd(TAG, "不支持指定的分辨率");
                    throw new ArgumentOutOfRangeException($"{width}x{height} is NOT supported.");
				}

				if (info.IsOpen && !info.IsPreviewing)
                {   //被open，但未取得影像时
                    info.SetSize(width, height);
					info.previewTexture = new Texture2D(
							width, height,
							TextureFormat.ARGB32,
							false, /* mipmap */
							true /* linear */);
					var nativeTexPtr = info.previewTexture.GetNativeTexturePtr();
                    AndroidDebug.Logd(TAG, "被open，但未取得影像时:"+ nativeTexPtr);
                    using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
					{
						clazz.CallStatic("setPreviewTexture",
							AndroidUtils.GetCurrentActivity(), deviceName,
							nativeTexPtr.ToInt32(),
							-1, //PreviewMode，-1：自动选择（Open时指定的PreferH 264标志有效）
                            width, height);
					}

					StartCoroutine(info.OnRender());
				}
			}
			else
			{
                AndroidDebug.Logd(TAG, "StartPreview：device name is empty/null");
                throw new ArgumentException("device name is empty/null");
			}
		}

        /**
         *结束从UVC设备/相机接收影像的请求
         *@param deviceName UVC设备识别字符串
         */
        private void StopPreview(string deviceName)
		{
            AndroidDebug.Logd(TAG, "结束从UVC设备/相机接收影像的请求：" + deviceName);
            var info = Get(deviceName);
			if (info != null)
			{
				info.SetSize(0, 0);
				StopCoroutine(info.OnRender());
				RequestStopPreview(deviceName);
			}
		}

        /**
		 *要求结束从UVC机器接收影像
		 * @param deviceName UVC设备识别字符串
		 */
        private void RequestStopPreview(string deviceName)
		{
            AndroidDebug.Logd(TAG, "要求结束从UVC机器接收影像：" + deviceName);
            if (!String.IsNullOrEmpty(deviceName))
			{
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					clazz.CallStatic("stopPreview",
						AndroidUtils.GetCurrentActivity(), deviceName);
				}
			}
		}

        /**
		 * 获取指定的UVC设备的信息（现在是vid和pid）作为UVC设备
		 * @param deviceName UVC设备识别字符串
		 */
        private UVCDevice GetDevice(string deviceName)
		{
            AndroidDebug.Logd(TAG, "获取指定的UVC设备的信息1：" + deviceName);

            if (!String.IsNullOrEmpty(deviceName))
			{
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
                    AndroidDebug.Logd(TAG, "获取指定的UVC设备的信息2：" + AndroidUtils.GetCurrentActivity());
                    AndroidDebug.Logd(TAG, "获取指定的UVC设备的信息2：" + clazz.CallStatic<string>("getInfo", AndroidUtils.GetCurrentActivity(), deviceName));
                    return UVCDevice.Parse(deviceName,
						clazz.CallStatic<string>("getInfo",
							AndroidUtils.GetCurrentActivity(), deviceName));
				}
			}
			else
			{
				throw new ArgumentException("device name is empty/null");
			}

		}

        /**
		 *获取指定的UVC设备的对应分辨率
         *@param deviceName UVC设备识别字符串
		 */
        private SupportedFormats GetSupportedVideoSize(string deviceName)
		{
            AndroidDebug.Logd(TAG, "获取指定的UVC设备的对应分辨率：" + deviceName);
            if (!String.IsNullOrEmpty(deviceName))
			{
				using (AndroidJavaClass clazz = new AndroidJavaClass(FQCN_PLUGIN))
				{
					return SupportedFormats.Parse(
						clazz.CallStatic<string>("getSupportedVideoSize",
							AndroidUtils.GetCurrentActivity(), deviceName));
				}
			}
			else
			{
				throw new ArgumentException("device name is empty/null");
			}
		}

        /**
		 *获取与指定的UVC识别字符串对应的CameraiInfo
        *如果尚未注册，则新建
        *@param deviceName UVC设备识别字符串
        *@param CameraInfo返回
		 */
        /*NonNull*/
        private CameraInfo CreateIfNotExist(string deviceName)
		{
            AndroidDebug.Logd(TAG, "获取与指定的UVC识别字符串对应的CameraiInfo：" + deviceName);
            if (!cameraInfos.ContainsKey(deviceName))
			{
				cameraInfos[deviceName] = new CameraInfo(GetDevice(deviceName));
			}
            return cameraInfos[deviceName];
		}

        /**
		 * 取得与指定的UVC识别字符串对应的CameraiInfo
        *@param deviceName UVC设备识别字母串
        *@param注册后返回CameraInfo，未注册则null
		 */
        /*Nullable*/
        private CameraInfo Get(string deviceName)
		{
			return !String.IsNullOrEmpty(deviceName) && cameraInfos.ContainsKey(deviceName) ? cameraInfos[deviceName] : null;
		}

        /**
		 *删除指定的UVC设备的CameraiInfo
        *@param deviceName UVC设备识别字母串
        *@param注册后返回CameraInfo，未注册则null
		 */
        /*Nullable*/
        private CameraInfo Remove(string deviceName)
		{
			CameraInfo info = null;

			if (cameraInfos.ContainsKey(deviceName))
			{
				info = cameraInfos[deviceName];
				cameraInfos.Remove(deviceName);
			}
	
			return info;
		}

        /**
		 *连接UVC设备时的处理实体
        *@param info
        *@return真：使用连接的UVC设备，假：不使用连接的UVC设备
		 */
        private bool HandleOnAttachEvent(CameraInfo info/*NonNull*/)
		{
			if ((UVCDrawers == null) || (UVCDrawers.Length == 0))
            {   //未分配IUVC Drawer时返回真（使用连接的UVC设备）
                return true;
			}
			else
			{
				bool hasDrawer = false;
				foreach (var drawer in UVCDrawers)
				{
					if (drawer is IUVCDrawer)
					{
						hasDrawer = true;
						if ((drawer as IUVCDrawer).OnUVCAttachEvent(this, info.device))
                        {   //如果其中一个IUVC Drawer回复真，则返回真（使用连接的UVC设备）
                            return true;
						}
					}
				}
                //未分配IUVC Drawer时返回真（使用连接的UVC设备）
                return !hasDrawer;
			}
		}

        /**
		 * UVC设备被拆除时的处理实体
		 * @param info
		 */
        private void HandleOnDetachEvent(CameraInfo info/*NonNull*/)
		{
			if ((UVCDrawers != null) && (UVCDrawers.Length > 0))
			{
				foreach (var drawer in UVCDrawers)
				{
					if (drawer is IUVCDrawer)
					{
						(drawer as IUVCDrawer).OnUVCDetachEvent(this, info.device);
					}
				}
			}
		}

	} // UVCManager

}   // namespace Serenegiant.UVC
