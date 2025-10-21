// 상단 using 유지
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using UnityEngine.TextCore; // OTL_FeatureTag

public class BossBase : MonoBehaviour, IDamageable
{
    [Header("Boss Stats")]
    public int maxHP = 100;
    protected int currentHP;

    [Header("UI(공유 참조)")]
    public Slider hpSlider;                          // 체력 슬라이더
    public TextMeshProUGUI hpText;                   // 숫자
    [Tooltip("보스바 루트(RectTransform). Screen Space - Camera 캔버스의 자식이어야 한다.")]
    public RectTransform bossBarRoot;                // 보스바 판넬
    [Tooltip("슬라이더 Fill 이미지(색상 변경용).")]
    public Image hpFill;                             

    [Header("보스 이름")]
    public string bossName = "보스";
    [Tooltip("보스 이름이 표시될 TMP 오브젝트")]
    public TextMeshProUGUI nameTextTarget;           // 이름 표기 대상

    [Header("색상 임계치(최대 4개)")]
    public Color defaultColor = new Color(0.2f,0.9f,0.2f,1f); // 기본 바 색
    [Serializable] public struct HpColorThreshold {
        [Tooltip("이 값 '이하'로 떨어지면 적용")]
        public int hpLessEqual;
        public Color color;
        public AudioClip sfx;                         // 선택: 색 바뀔 때 효과음
        public ParticleSystem fx;                    // 선택: 이펙트
    }
    public HpColorThreshold[] thresholds = new HpColorThreshold[4]; // 0~3개 사용 가능

    [Header("연출(등장/퇴장)")]
    public float barAnimTime = 0.35f;                // 바 슬라이드 시간
    public Vector2 barOnscreenPos = new Vector2(0, -40);      // 화면 상단 기준 목표 앵커 위치
    public Vector2 barOffscreenPos = new Vector2(0, +120);    // 화면 위로 숨김 위치
    public AudioClip sfxAppear;
    public AudioClip sfxDisappear;
    public ParticleSystem fxAppear;
    public ParticleSystem fxDisappear;
    public AudioSource audioSrc;                     // 없으면 자동 추가
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1); // 슬라이드 이징
    public float colorFadeTime = 0.25f;   // 체력색 전환 페이드 시간
    public float shakeAmp = 6f;           // 바 흔들림(픽셀)
    public float shakeFreq = 30f;         // 흔들림 주파수
    public bool shakeOnAppear = true;     // 등장 시 잠깐 흔들기
    public float shakeOnAppearTime = 0.2f;


    [Header("Death")]
    public float deathDelay = 0f;

    public event Action<int,int> OnHpChanged;
    public event Action<BossBase> OnBossDie;

    private DamageNumberPool _dmgPool;
    /// <summary>HP 0→최대까지 n초 동안 채워지는 연출</summary>
    IEnumerator CoChargeHP(float dur)
{
    int from = 0, to = maxHP;
    currentHP = from; UpdateUI();
    float t = 0f;
    while (t < dur)
    {
        t += Time.unscaledDeltaTime;
        currentHP = Mathf.RoundToInt(Mathf.Lerp(from, to, t / dur));
        UpdateUI();
        yield return null;
    }
    currentHP = to; UpdateUI();
}

    void Awake()
    {
        if (!audioSrc) { audioSrc = gameObject.AddComponent<AudioSource>(); audioSrc.playOnAwake = false; }
    }

    protected virtual void Start()
    {
        currentHP = maxHP;
        if (nameTextTarget) nameTextTarget.text = bossName;
        UpdateUI();

        // 숫자 겹침 방지(기존 유지)
        if (hpText)
        {
            hpText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            hpText.ForceMeshUpdate(true);
        }

        // ★ 보스바는 ‘처음엔 숨김’
        if (bossBarRoot)
        {
            bossBarRoot.anchoredPosition = barOffscreenPos;  // 주석: 화면 위로 숨김
            bossBarRoot.gameObject.SetActive(false);         // 주석: 비활성
        }

        ApplyBarColor();
    }
    public void ShowBarWithCharge(float seconds)
    {
        if (bossBarRoot)
        {
            bossBarRoot.gameObject.SetActive(true);  // ★ 주석: 먼저 켠다
            StopAllCoroutines();
            StartCoroutine(BarSlide(true));          // 주석: 슬라이드 인
        }
        StopCoroutine(nameof(CoChargeHP));
        StartCoroutine(CoChargeHP(Mathf.Max(0.05f, seconds))); // 주석: 0→최대 충전
    }

    public void SetUI(Slider slider, TextMeshProUGUI text)
    {
        hpSlider = slider; hpText = text; UpdateUI();
    }

    public void InitHP(int newMaxHP, int? newCurrentHP = null, bool clamp = true)
    {
        maxHP = Mathf.Max(1, newMaxHP);
        currentHP = newCurrentHP.HasValue ? newCurrentHP.Value : maxHP;
        if (clamp) currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateUI(); OnHpChanged?.Invoke(currentHP, maxHP);
    }

    public void TakeDamage(int amount, bool weak, float weakMultiplier)
    {
        int final = amount + (weak ? Mathf.RoundToInt(weakMultiplier) : 0); // 약점=보너스 더하기
        final = Mathf.Max(0, final);

        currentHP = Mathf.Max(0, currentHP - final);
        UpdateUI();
        OnHpChanged?.Invoke(currentHP, maxHP);

        if (_dmgPool != null)
        {
            Vector3 worldPos = transform.position + Vector3.up * 0.6f;
            _dmgPool.Spawn(worldPos, final, weak ? Color.yellow : Color.white);
        }

        if (currentHP == 0) Die();
        else ApplyBarColor(); // 색 갱신
    }

    protected virtual void Die()
    {
        // 퇴장 연출(바 먼저 숨김)
        if (bossBarRoot) StartCoroutine(BarSlide(false));
        if (fxDisappear) Instantiate(fxDisappear, transform.position, Quaternion.identity);
        if (audioSrc && sfxDisappear) audioSrc.PlayOneShot(sfxDisappear);

        OnBossDie?.Invoke(this);
        Destroy(gameObject, Mathf.Max(0f, deathDelay));
    }
    public void HideBar()
    {
        if (bossBarRoot)
        {
            StopAllCoroutines();
            StartCoroutine(BarSlide(false));         // 주석: 슬라이드 아웃
            // 슬라이드 종료 후 비활성화(코루틴 끝에서 처리)
        }
    }

    protected void UpdateUI()
    {
    
        if (hpSlider) { hpSlider.maxValue = maxHP; hpSlider.value = currentHP; }
        if (hpText)   { hpText.text = currentHP.ToString("D0"); hpText.ForceMeshUpdate(); }
        if (nameTextTarget) nameTextTarget.text = bossName;
    }
    // 외부 스크립트가 GetCurrentHP()/GetMaxHP()를 호출하는 프로젝트 호환용
    public int GetCurrentHP() { return currentHP; }   // 현재 HP 반환
    public int GetMaxHP()     { return maxHP; }       // 최대 HP 반환

    // 프로퍼티도 제공(신규 코드에서 사용할 것)
    public int CurrentHP => currentHP;                // 현재 HP
    public int MaxHP     => maxHP;                    // 최대 HP
    public bool IsDead   => currentHP <= 0;           // 사망 여부
    // === 색 임계치 처리 ===
    void ApplyBarColor()
    {
        if (!hpFill) return;
        // 목표 색 결정
        Color target = defaultColor;
        int cur = currentHP;
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (thresholds[i].hpLessEqual <= 0) continue;
            if (cur <= thresholds[i].hpLessEqual)
            {
                target = thresholds[i].color;
                if (audioSrc && thresholds[i].sfx) audioSrc.PlayOneShot(thresholds[i].sfx);
                if (thresholds[i].fx) Instantiate(thresholds[i].fx, transform.position, Quaternion.identity);
                break;
            }
        }
        // 색 페이드 코루틴
        StopCoroutine(nameof(CoFadeFill));
        StartCoroutine(CoFadeFill(hpFill.color, target, colorFadeTime));
    }
    IEnumerator CoFadeFill(Color from, Color to, float t)
    {
        if (t <= 0f) { hpFill.color = to; yield break; }
        float e = 0f;
        while (e < t)
        {
            e += Time.unscaledDeltaTime;
            float k = e / t;
            hpFill.color = Color.LerpUnclamped(from, to, k);
            yield return null;
        }
        hpFill.color = to;
    }


    // === 보스바 슬라이드 ===
    IEnumerator BarSlide(bool show)
    {
        if (!bossBarRoot) yield break;

        Vector2 from = show ? barOffscreenPos : barOnscreenPos;
        Vector2 to   = show ? barOnscreenPos : barOffscreenPos;

        float dur = Mathf.Max(0.05f, barAnimTime);
        float t = 0f;

        // SFX/FX
        if (audioSrc && (show ? sfxAppear : sfxDisappear))
            audioSrc.PlayOneShot(show ? sfxAppear : sfxDisappear);
        if (show && fxAppear) Instantiate(fxAppear, Camera.main ? Camera.main.transform.position : transform.position, Quaternion.identity);
        if (!show && fxDisappear) Instantiate(fxDisappear, transform.position, Quaternion.identity);

        // 등장 시 짧은 흔들림 예약
        float shakeLeft = (show && shakeOnAppear) ? shakeOnAppearTime : 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = ease != null ? ease.Evaluate(k) : k;

            // 기본 위치
            Vector2 p = Vector2.LerpUnclamped(from, to, e);

            // 흔들림(옵션)
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.unscaledDeltaTime;
                float wob = Mathf.Sin(Time.unscaledTime * shakeFreq) * shakeAmp * (shakeLeft / Mathf.Max(0.0001f, shakeOnAppearTime));
                p.x += wob; // 좌우 흔들림
            }

            bossBarRoot.anchoredPosition = p;
            yield return null;
        }
        bossBarRoot.anchoredPosition = to;
        if (!show) bossBarRoot.gameObject.SetActive(false);
    }


    // === 디버그: Q로 등장/퇴장 스케치 ===
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // 이미 화면에 있으면 숨기기, 아니면 등장 슬라이드
            if (bossBarRoot)
            {
                bool on = Vector2.Distance(bossBarRoot.anchoredPosition, barOnscreenPos) < 0.5f;
                StopAllCoroutines();
                StartCoroutine(BarSlide(!on));
            }
            // 보스 등장 이펙트
            if (fxAppear) Instantiate(fxAppear, transform.position, Quaternion.identity);
            if (audioSrc && sfxAppear) audioSrc.PlayOneShot(sfxAppear);
        }
    }
}