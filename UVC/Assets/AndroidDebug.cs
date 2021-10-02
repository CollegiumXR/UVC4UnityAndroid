using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/****************************************************
	作者：cg
	功能：Android调试
*****************************************************/
public static class AndroidDebug
{
	public static void Logd(string Tag, string msg)
    {
        Debug.Log(Tag + "调试" + msg);
    }
}
