using UnityEngine;
using Newtonsoft.Json.Linq;
using System;

/*
 * THETA V
 * {"vid":1482,"pid":872}
 */

namespace Serenegiant.UVC
{

	[Serializable]
	public class UVCDevice
	{
		public readonly string deviceName;
		public readonly int vid;
		public readonly int pid;
		public readonly string name;

		public static UVCDevice Parse(string deviceName, string jsonString)
		{
            AndroidDebug.Logd("Parse", "deviceName：" + jsonString);
            UVCDevice result;
			try
			{
                //var element = JsonDocument.Parse(jsonString).RootElement;
                //JsonElement v;
                //string name;
                //if (element.TryGetProperty("name", out v))
                //{
                //	name = v.GetString();
                //} else
                //{
                //	name = null;
                //}

                //result = new UVCDevice(deviceName,
                //element.GetProperty("vid").GetInt32(),
                //element.GetProperty("pid").GetInt32(), name);

                JToken element = JObject.Parse(jsonString).Root;
                string name;
                Debug.Log("获取根节点：" + element.ToString());
                //if(element["name"].HasValues)
                //{
                //    name = element["name"].ToString();
                //    AndroidDebug.Logd("Parse", "deviceName：" + name);
                //}
                //else
                //{
                    name = null;
                //    AndroidDebug.Logd("Parse", "deviceName：" + name);
                //}
                result = new UVCDevice(
                    deviceName,
                    int.Parse(element["vid"].ToString()),
                    int.Parse(element["pid"].ToString()),
                    name);

            }
			catch (Exception e)
			{
				throw new ArgumentException(e.ToString());
			}

			if (result == null)
			{
				throw new ArgumentException($"failed to parse ({jsonString})");
			}
			return result;
		}

		public UVCDevice(string deviceName, int vid, int pid, string name)
		{
			this.deviceName = deviceName;
			this.vid = vid;
			this.pid = pid;
			this.name = name;
		}

		public override string ToString()
		{
			return $"{base.ToString()}(deviceName={deviceName},vid={vid},pid={pid},name={name})";
		}


		/**
		 * Ricohの製品かどうか
		 * @param info
		 */
		public bool IsRicoh
		{
			get { return (vid == 1482); }
		}

		/**
		 * THETA Sかどうか
		 * @param info
		 */
		public bool IsTHETA_S
		{
			get { return (vid == 1482) && (pid == 10001); }
		}

		/**
		 * THETA Vかどうか
		 * @param info
		 */
		public bool IsTHETA_V
		{
			// THETA Vからのpid=872は動かない
			get { return (vid == 1482) && (pid == 10002); }
		}
	} // UVCDevice

} // namespace Serenegiant.UVC

