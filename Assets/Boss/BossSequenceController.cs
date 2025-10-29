using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // ← 맨 위 using 목록에 추가

public class BossSequenceController : MonoBehaviour
{
    [Header("① 보스 스폰")]
    public GameObject bossPrefab;
    public Vector3 bossWorldPos = new Vector3(8,2,0);

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
    BossBase boss;

    void Update()
    {
        if (!spawned && Input.GetKeyDown(KeyCode.Q)) SpawnBossOnce();
    }
    void SpawnBossOnce()
    {
        spawned = true;

        if (!bossPrefab) { Debug.LogError("[BossSeq] bossPrefab 미지정"); return; }
        var go = Instantiate(bossPrefab, bossWorldPos, Quaternion.identity);

        // ■ 방금 소환한 프리팹에서 BossBase 직접 찾기(씬 내 다른 BossBase와 혼동 방지)
        boss = go.GetComponentInChildren<BossBase>();
        if (!boss)
            boss = UnityEngine.Object.FindFirstObjectByType<BossBase>(FindObjectsInactive.Exclude);

        // 액터 바인딩
        boss.BindActor(go.transform);

        // ■ 보스바 UI 자동 연결(씬마다 참조가 비어 있어도 동작)
        AutoWireBossBarUI();

        // UI 주입 + 연출 파라미터 동기화
        boss.bossBarRoot = bossBarRoot;
        boss.hpSlider = hpSlider;
        boss.hpText = hpText;
        boss.hpFill = hpFill;
        boss.nameTextTarget = nameText;
        boss.barAnimTime = barAppearTime;

        // ■ 보스바 오브젝트를 확실히 활성화
        if (bossBarRoot) bossBarRoot.gameObject.SetActive(true);

        // 등장 연출
        boss.ShowBarWithCharge(chargeSeconds);
        if (sfxIntro) AudioSource.PlayClipAtPoint(sfxIntro, Camera.main ? Camera.main.transform.position : transform.position, 1f);
        if (fxIntro) Instantiate(fxIntro, go.transform.position, Quaternion.identity);

        // BGM 시작
        StartOrSwapBgm(bgmClip, loopBgm);

        // 임계치 BGM 교체 감시
        boss.OnBgmSwapRequest += OnBgmSwapRequest;
        boss.OnBossDie += OnBossDie;
    }
    // ▼ 보스바 UI 자동 탐색/바인딩(씬 이름이 달라도 동작)
    //   - bossBarRoot가 비어 있으면 "BossBarRoot" 이름 검색
    //   - Slider/Text/Image는 자식에서 자동 획득
    void AutoWireBossBarUI()
    {
        // 1) 루트 찾기
        if (!bossBarRoot)
        {
            var go = GameObject.Find("BossBarRoot"); // 캔버스 안의 루트 이름을 "BossBarRoot"로 권장
            if (go) bossBarRoot = go.GetComponent<RectTransform>();
            if (!bossBarRoot)
            {
                // 캔버스가 하나뿐이면 그 아래에서 탐색
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                if (canvas)
                {
                    var t = canvas.transform.Find("BossBarRoot");
                    if (t) bossBarRoot = t.GetComponent<RectTransform>();
                }
            }
        }

        // 2) 기본 컴포넌트 연결
        if (bossBarRoot)
        {
            if (!hpSlider) hpSlider = bossBarRoot.GetComponentInChildren<Slider>(true);

            if (!hpText || !nameText)
            {
                var texts = bossBarRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                // 이름 힌트 우선
                foreach (var t in texts)
                {
                    var n = t.name.ToLower();
                    if (!hpText && (n.Contains("hp") || n.Contains("value"))) hpText = t;
                    if (!nameText && (n.Contains("name") || n.Contains("title"))) nameText = t;
                }
                // 그래도 없으면 순서로 보정
                if (!hpText && texts.Length > 0) hpText = texts[0];
                if (!nameText && texts.Length > 1) nameText = texts[1];
            }

            if (!hpFill && hpSlider && hpSlider.fillRect)
                hpFill = hpSlider.fillRect.GetComponent<UnityEngine.UI.Image>();
        }

        // 3) 최종 검증 로그
        if (!bossBarRoot || !hpSlider)
            Debug.LogError("[BossSeq] BossBar UI 미설정. bossBarRoot/Slider 확인(이름 'BossBarRoot' 권장).");
    }


    void StartOrSwapBgm(AudioClip clip, bool loop)
    {
        if (!clip) return;
        if (!bgmSource)
        {
            var src = new GameObject("BGM_Source").AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            bgmSource = src;
        }
        bgmSource.loop = loop;
        bgmSource.clip = clip;
        bgmSource.Play();
    }
    void OnBgmSwapRequest(AudioClip clip, bool loop)
    {
        StartOrSwapBgm(clip, loop);
    }
    void OnBossDie(BossBase b)
    {
        if (bgmSource && bgmSource.isPlaying) bgmSource.Stop();
        boss.OnBossDie -= OnBossDie;
       // spawned = false; // ← 같은 씬에서 재소환 테스트가 필요할 때 Q 재허용
    }
    // ▼ 씬 로드마다 Q 게이트 리셋
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetSpawnGate(); // 현재 씬에서도 즉시 리셋
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ▼ 새 씬 들어오면 1회 소환 가능 상태로 복구
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetSpawnGate();
    }

    // ▼ 공통 리셋 함수
    void ResetSpawnGate()
    {
        spawned = false; // Q 재작동 허용
    }
}
