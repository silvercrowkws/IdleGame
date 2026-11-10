using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public static class MicPermission
{
    private const string AskedKey = "mic_permission_asked";

    /// <summary>
    /// 지금 마이크를 써도 되는지
    /// </summary>
    public static bool IsGranted
    {
        get
        {
#if UNITY_EDITOR
            return Microphone.devices.Length > 0;
#elif UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// 게임 처음 시작할 때 한 번만 권한을 요청한다. 이미 물어봤으면 다시 묻지 않는다.
    /// </summary>
    public static IEnumerator RequestOnce()
    {
        if (IsGranted) yield break;                              // 이미 허용됨
        if (PlayerPrefs.GetInt(AskedKey, 0) == 1) yield break;   // 이미 한 번 물어봄

        PlayerPrefs.SetInt(AskedKey, 1);
        PlayerPrefs.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
    var callbacks = new PermissionCallbacks();
    callbacks.PermissionGranted += p => Debug.Log("마이크 허용: " + p);
    callbacks.PermissionDenied += p => Debug.Log("마이크 거절: " + p);
    callbacks.PermissionDeniedAndDontAskAgain += p => Debug.Log("마이크 거절(다시 묻지 않음): " + p);
    Permission.RequestUserPermissions(new[] { Permission.Microphone }, callbacks);
    yield break;   // 결과를 기다리며 반복하지 않음
#elif UNITY_IOS && !UNITY_EDITOR
    yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#else
        yield break;
#endif
    }
}