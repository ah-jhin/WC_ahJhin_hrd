using System;                     // Action<>, Delegate
using System.Reflection;          // BindingFlags
using UnityEngine;                // AudioClip 등
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // ← 맨 위 using 목록에 추가

public class BossSequenceController : MonoBehaviour
{
    [Header("① 보스 스폰")]
    public GameObject bossPrefab;
    public Vector3 bossWorldPos = new Vector3(8,2,0);
    BossBase boss;                            // 씬 Controller에 붙어 있는 BossBase
    Transform currentActor;                   // 현재 소환된 보스 외형 루트

    [Header("② 보스바 UI(Screen Space - Camera 캔버스)")]
    public RectTransform bossBarRoot;
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Image hpFill;
    public TextMeshProUGUI nameText;

    [Header("③ 보스바 연출")]
    public float barAppearTime = 0.35f;
    public float chargeSeconds = 1.5f;
    public AudioClip sfxIntro;
    public ParticleSystem fxIntro;

    [Header("④ BGM")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;
    public bool loopBgm = true;
    bool spawned = false;

    void Awake()
    {
        // 씬의 Controller 오브젝트에 붙어 있는 BossBase를 참조
        boss = GetComponent<BossBase>();
        if (!boss) Debug.LogError("[BossSeq] 씬 Controller에 BossBase가 필요합니다.");
    }

    void Update()
    {
        if (!spawned && Input.GetKeyDown(KeyCode.Q)) SpawnBossOnce();
    }
    // 클래스 내부에 추가
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded; // ★ 구독
        ResetSpawnGate();                          // ★ 에디터에서 단독 실행해도 초기화
    }
    // 보스는 한 번만 소환
    void SpawnBossOnce()
    {
        if (spawned) return;
        spawned = true;

        if (!bossPrefab) { Debug.LogError("[BossSeq] bossPrefab 미지정"); spawned = false; return; }
        if (!boss) { Debug.LogError("[BossSeq] 씬의 BossBase 참조 없음"); spawned = false; return; }

        // 1) 프리팹 인스턴스화(외형/애니/콜라이더만 들어있는 오브젝트)
        var go = Instantiate(bossPrefab, bossWorldPos, Quaternion.identity);
        currentActor = go.transform;

        // 2) 씬의 BossBase에 '배우(Actor) Transform' 바인딩
        //    - 우선 BindActor(Transform) 메서드가 있으면 호출
        //    - 없으면 자주 쓰는 필드명(actor, actorRoot, modelRoot)을 리플렉션으로 세팅
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var m = boss.GetType().GetMethod("BindActor", flags);
        if (m != null) m.Invoke(boss, new object[] { currentActor });
        else
        {
            var f = boss.GetType().GetField("actor", flags)
                 ?? boss.GetType().GetField("actorRoot", flags)
                 ?? boss.GetType().GetField("modelRoot", flags);
            if (f != null) f.SetValue(boss, currentActor);
            else Debug.LogWarning("[BossSeq] BossBase에 BindActor/actor/actorRoot/modelRoot가 없어 직접 바인딩했습니다. 필요 시 BossBase에 public void BindActor(Transform t) 추가 권장.");
        }

        // 3) 보스바 UI 자동 배선(마커 방식 사용 중이라면 그대로 둠)
        AutoWireBossBarUI();
        if (bossBarRoot) bossBarRoot.gameObject.SetActive(true);

        // 4) BossBase에 UI 전달(이미 BossBase가 공개 필드/프로퍼티를 가지고 있다면 그대로 할당)
        //    필드명이 다르면 생략 가능. 사용 중인 이름에 맞춰 연결해 두면 된다.
        TrySetField("bossBarRoot", bossBarRoot);
        TrySetField("hpSlider", hpSlider);
        TrySetField("hpText", hpText);
        TrySetField("hpFill", hpFill);
        TrySetField("nameTextTarget", nameText);
        TrySetField("barAnimTime", barAppearTime);

        // 5) 인트로 연출 및 BGM 교체
        CallMethodIfExists("ShowBarWithCharge", chargeSeconds); // BossBase.ShowBarWithCharge(float)
        StopAllLoopingBgm(null);                                // 기존 루프 BGM 정지
        StartOrSwapBgm(bgmClip, loopBgm);                       // 스테이지 BGM 시작

        // 6) 죽음/교체 이벤트 구독(있을 때만)
        // BossBase 이벤트 직접 구독(중복 방지 포함)
        if (boss != null)
        {
            // 보스가 BGM 교체를 요구할 때
            boss.OnBgmSwapRequest -= HandleBgmSwap;   // 중복 제거
            boss.OnBgmSwapRequest += HandleBgmSwap;   // 등록

            // 보스가 사망했을 때
            boss.OnBossDie -= HandleBossDie;   // 중복 제거
            boss.OnBossDie += HandleBossDie;   // 등록
        }

        // --- 로컬 유틸: 리플렉션 헬퍼 ---
        void TrySetField(string name, object value)
        {
            var f2 = boss.GetType().GetField(name, flags);
            if (f2 != null) f2.SetValue(boss, value);
        }
        void CallMethodIfExists(string name, params object[] args)
        {
            var mm = boss.GetType().GetMethod(name, flags);
            if (mm != null) mm.Invoke(boss, args);
        }
    }


    // ▼ 보스바 UI 자동 탐색/바인딩 ( BossBarMarker 사용)
    void AutoWireBossBarUI()
    {
        // 1) 루트 찾기: 씬 어디든 BossBarMarker가 붙은 오브젝트를 탐색
#if UNITY_2023_1_OR_NEWER
    var marker = UnityEngine.Object.FindFirstObjectByType<BossBarMarker>(FindObjectsInactive.Include);
#else
#pragma warning disable CS0618
        var marker = UnityEngine.Object.FindObjectOfType<BossBarMarker>();
#pragma warning restore CS0618
#endif
        if (marker) bossBarRoot = marker.GetComponent<RectTransform>();

        // 2) 기본 컴포넌트 연결: 이름/경로 무시하고 타입으로만 획득
        if (bossBarRoot)
        {
            if (!hpSlider) hpSlider = bossBarRoot.GetComponentInChildren<Slider>(true);
            if (!hpFill && hpSlider && hpSlider.fillRect)
                hpFill = hpSlider.fillRect.GetComponent<Image>();

            if (!hpText || !nameText)
            {
                var texts = bossBarRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                if (!hpText && texts.Length > 0) hpText = texts[0];   // 첫 번째를 HP 텍스트로
                if (!nameText && texts.Length > 1) nameText = texts[1]; // 두 번째를 이름으로
            }
        }

        // 3) 최종 검증
        if (!bossBarRoot || !hpSlider)
            Debug.LogError("[BossSeq] BossBar UI 미설정. BossBarMarker를 보스바 루트에 붙여라.");
    }


    // ★ 씬 내 루프 BGM 전부 정지(메뉴/스테이지 중복 방지)
    // ★ 태그 사용 금지. 루프 소스만 정지. except는 건너뜀.
    void StopAllLoopingBgm(AudioSource except)
    {
        var list = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var a in list)
        {
            if (!a || a == except) continue;

            // 루프 음악만 정지. 필요하면 이름 규칙도 추가 가능.
            bool looksLikeBgm =
                a.loop
                || (a.outputAudioMixerGroup != null && a.outputAudioMixerGroup.name.Contains("BGM"))
                || a.gameObject.name.Contains("[BGM]"); // 선택 규칙

            if (looksLikeBgm) a.Stop();
        }
    }

    void StartOrSwapBgm(AudioClip clip, bool loop)
    {
        if (!clip) return;

        if (!bgmSource)
        {
            var go = new GameObject("BGM_Source");
            go.transform.SetParent(transform, false);
            bgmSource = go.AddComponent<AudioSource>();
            bgmSource.spatialBlend = 0f;
            bgmSource.playOnAwake = false;
            // ★ 삭제: bgmSource.tag = "BGM";
        }

        StopAllLoopingBgm(except: bgmSource);

        bgmSource.loop = loop;
        bgmSource.clip = clip;
        bgmSource.Stop();
        bgmSource.Play();
    }

    void OnBgmSwapRequest(AudioClip clip, bool loop)
    {
        StartOrSwapBgm(clip, loop);
    }

    // 보스가 BGM 교체를 요청했을 때 실행
    private void HandleBgmSwap(AudioClip clip, bool loop)
    {
        StartOrSwapBgm(clip, loop); // // BGM 교체 유틸
    }

    // 보스 사망 시 호출
    private void HandleBossDie(BossBase _)
    {
        if (bgmSource && bgmSource.isPlaying) bgmSource.Stop(); // Stage BGM 즉시 정지
    }

    // ★ 씬 이탈/비활성 시 안전 정지
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (bgmSource) bgmSource.Stop();   // 메뉴로 돌아가도 잔류 재생 방지
    }

    // ▼ 새 씬 들어오면 1회 소환 가능 상태로 복구
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetSpawnGate();
    }

    // ▼ 공통 리셋 함수
    void ResetSpawnGate()
    {
        spawned = false;                       // Q 재작동 허용
                                               // 이전 씬에서 넘어오며 남았을 수 있는 보스 외형 정리
        if (currentActor)                      // null 체크
        {
            Destroy(currentActor.gameObject);  // 혹시 남아있으면 제거
            currentActor = null;
        }
    }

}
