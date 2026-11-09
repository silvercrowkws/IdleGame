using System.Collections;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class BreathInput : MonoBehaviour
{
    [Header("민감도 (기기마다 마이크 감도가 달라서 조절 필요)")]
    [SerializeField] private float threshold = 0.10f;   // 이 이상이면 '분다'고 판단
    [SerializeField] private float fullAt = 0.40f;      // 이 값이면 최대 세기
    [SerializeField, Range(0f, 1f)] private float smoothing = 0.35f;

    [Header("테스트용")]
    [SerializeField, Range(0f, 1f)] private float debugPower = 0f;   // 재생 중 슬라이더로 바람 흉내
    [SerializeField] private TMP_Text debugText;                      // (선택) 화면에 마이크 값 표시

    /// <summary>
    /// 현재 바람 세기 (0~1)
    /// </summary>
    public float Power { get; private set; }

    private const int SampleRate = 16000;

    private AudioClip clip;
    private string device;           // null = 기본 마이크
    private bool listening;
    private bool resumeListening;
    private float level;
    private readonly float[] buf = new float[512];
    private Coroutine startRoutine;

    public void StartListening()
    {
        if (listening || startRoutine != null) return;
        startRoutine = StartCoroutine(StartRoutine());
    }

    public void StopListening()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }
        if (Microphone.IsRecording(device)) Microphone.End(device);

        listening = false;
        clip = null;
        level = 0f;
        Power = 0f;
        if (debugText != null) debugText.text = "";
    }

    private IEnumerator StartRoutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            float t = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;   // 사용자가 허용할 때까지 대기
            }
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            startRoutine = null;
            yield break;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            startRoutine = null;
            yield break;
        }
#endif

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("마이크 장치가 없습니다.");
            startRoutine = null;
            yield break;
        }

        device = null;
        clip = Microphone.Start(device, true, 1, SampleRate);
        if (clip == null)
        {
            Debug.LogWarning("마이크를 시작하지 못했습니다.");
            startRoutine = null;
            yield break;
        }

        // 마이크가 실제로 녹음을 시작할 때까지 잠깐 대기
        float wait = 0f;
        while (Microphone.GetPosition(device) <= 0 && wait < 2f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        listening = Microphone.GetPosition(device) > 0;
        startRoutine = null;
    }

    private void Update()
    {
        float raw = 0f;

        if (listening && clip != null)
        {
            int pos = Microphone.GetPosition(device);
            if (pos >= buf.Length)
            {
                clip.GetData(buf, pos - buf.Length);   // 가장 최근 소리 일부를 읽음
                float sum = 0f;
                for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
                raw = Mathf.Sqrt(sum / buf.Length);    // 소리 크기(RMS)
            }
        }

        level = Mathf.Lerp(level, raw, smoothing);     // 값이 튀지 않게 부드럽게

        float p = Mathf.InverseLerp(threshold, fullAt, level);
        Power = Mathf.Max(p, debugPower);

        if (debugText != null && listening)
            debugText.text = $"마이크 {level:F3}   세기 {Power:F2}";
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            resumeListening = listening;
            StopListening();
        }
        else if (resumeListening)
        {
            resumeListening = false;
            StartListening();
        }
    }

    private void OnDisable()
    {
        StopListening();
    }
}