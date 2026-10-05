using System.Collections;
using UnityEngine;

/// <summary>
/// BGM과 효과음(SFX) 재생을 총괄하는 싱글톤. 스토리 씬, 리듬게임 플레이 씬 등
/// 어디서든 AudioManager.Instance.PlayBGM(...) / PlaySFX(...) 형태로 호출해서 쓴다.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // 싱글톤
    #region Singleton
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }
    #endregion

    [Header("오디오 소스 (비워두면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("볼륨 (0~1)")]
    [Range(0f, 1f)][SerializeField] private float bgmVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

    [Header("기본 크로스페이드 시간 (초)")]
    [SerializeField] private float defaultFadeDuration = 1f;

    private AudioClip _currentBgmClip;
    private Coroutine _fadeCoroutine;

    // 브금 재생
    #region BGM
    /// <summary>
    /// BGM을 재생한다. 이미 같은 클립이 재생 중이면 아무 것도 하지 않는다 (덜컹거림 방지).
    /// </summary>
    public void PlayBGM(AudioClip clip, bool loop = true, float fadeDuration = -1f)
    {
        if (_currentBgmClip == clip && bgmSource.isPlaying) return;

        if (clip == null) return;

        if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

        _currentBgmClip = clip;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(CrossFadeBGM(clip, loop, fadeDuration));
    }

    public void StopBGM(float fadeDuration = -1f)
    {
        if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

        _currentBgmClip = null;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    private IEnumerator CrossFadeBGM(AudioClip newClip, bool loop, float fadeDuration)
    {
        // 기존 곡이 재생 중이면 먼저 페이드 아웃
        if (bgmSource.isPlaying)
        {
            yield return Fade(bgmSource, bgmSource.volume, 0f, fadeDuration * 0.5f);
        }

        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.volume = 0f;
        bgmSource.Play();

        yield return Fade(bgmSource, 0f, bgmVolume, fadeDuration * 0.5f);

        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutAndStop(float fadeDuration)
    {
        yield return Fade(bgmSource, bgmSource.volume, 0f, fadeDuration);
        bgmSource.Stop();
        _fadeCoroutine = null;
    }

    private IEnumerator Fade(AudioSource source, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        source.volume = to;
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource.isPlaying) bgmSource.volume = bgmVolume;
    }
    #endregion

    // 효과음 재생
    #region SFX
    /// <summary>
    /// 짧은 효과음 재생 (버튼 클릭, 판정음 등). 여러 개가 겹쳐도 서로 끊기지 않는다.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
    #endregion
}