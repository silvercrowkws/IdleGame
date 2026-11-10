using UnityEngine;

public class StartupPermissions : MonoBehaviour
{
    // 첫 씬이 로드되기 직전(유니티 로고가 나오는 동안)에 자동으로 실행됨
    // 씬에 따로 배치하지 않아도 됨.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("StartupPermissions");
        DontDestroyOnLoad(go);
        go.AddComponent<StartupPermissions>();
    }

    private void Awake()
    {
        // 지연 없이 바로 요청 (이미 허용했거나 한 번 물어봤다면 아무것도 안 함)
        StartCoroutine(Run());
    }

    private System.Collections.IEnumerator Run()
    {
        yield return MicPermission.RequestOnce();
        Destroy(gameObject);   // 일을 마치면 스스로 사라짐
    }
}