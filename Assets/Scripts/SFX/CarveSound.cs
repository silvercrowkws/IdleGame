using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CarveSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 0.2f)] private float pitchRange = 0.04f;   // 높낮이를 살짝 흔듦
    [SerializeField, Range(0.1f, 1f)] private float volume = 0.8f;

    [Header("재생 타이밍")]
    [SerializeField] private float minMoveToStart = 3f;                   // 소리가 끝난 뒤 이만큼 움직이면 다음 소리 (이미지 픽셀)
    [SerializeField, Range(0.5f, 1f)] private float overlapRatio = 0.85f; // 소리가 이만큼 재생되면 다음 소리를 받을 수 있음 (작을수록 이어짐)

    private AudioSource src;
    private float playingUntil;   // 이 시각까지는 재생 중으로 본다
    private float moveAcc;
    private int lastIndex = -1;

    private void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
    }

    // 새로 누를 때: 이전 소리가 남아 있어도 첫 움직임에서 바로 소리가 나게 함
    public void StartStroke()
    {
        playingUntil = 0f;
        moveAcc = minMoveToStart;
    }

    // 파는 동안 매번 호출됨 (dist: 이번에 움직인 거리)
    public void Feed(float dist)
    {
        // 소리가 재생 중이면 기다림 (이 동안의 움직임은 세지 않음)
        if (Time.unscaledTime < playingUntil)
        {
            moveAcc = 0f;
            return;
        }

        // 소리가 끝난 뒤 조금이라도 움직이면 다음 소리 재생
        moveAcc += dist;
        if (moveAcc < minMoveToStart) return;

        moveAcc = 0f;
        Play();
    }

    private void Play()
    {
        if (clips == null || clips.Length == 0) return;

        int i;
        do { i = Random.Range(0, clips.Length); }
        while (clips.Length > 1 && i == lastIndex);   // 같은 소리가 연달아 나오지 않게
        lastIndex = i;

        src.pitch = 1f + Random.Range(-pitchRange, pitchRange);
        src.PlayOneShot(clips[i], volume);

        // 이 소리가 끝나는 시각 (살짝 일찍 다음 소리를 받아서 자연스럽게 이어지게)
        playingUntil = Time.unscaledTime + clips[i].length / src.pitch * overlapRatio;
    }
}