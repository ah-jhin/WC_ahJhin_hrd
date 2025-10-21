// Assets/Boss/BossSequenceController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Q를 "한 번" 눌렀을 때: 보스 스폰 + 보스바 등장 + HP충전 + BGM 재생.
/// HP=0이 되면 BossBase가 알아서 HideBar(), Die() 호출. 여기선 BGM만 정리.
/// </summary>
public class BossSequenceController : MonoBehaviour
{
    [Header("보스 스폰")]
    public GameObject bossPrefab;        // BossBase가 붙은 프리팹
    public Vector3 bossWorldPos = new Vector3(8, 2, 0); // 고정 등장 위치

    [Header("보스바(UI 참조) - Screen Space Camera 캔버스의 자식")]
    public RectTransform bossBarRoot;
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Image hpFill;
    public TextMeshProUGUI nameText;

    [Header("연출")]
    public float barAppearTime = 0.35f;  // 슬라이드 시간
    public float chargeSeconds = 1.5f;   // HP 충전 연출 시간
    public AudioClip sfxIntro;           // 등장음
    public ParticleSystem fxIntro;       // 등장 이펙트

    [Header("BGM")]
    public AudioSource bgmSource;        // 카메라나 전용 오브젝트의 AudioSource
    public AudioClip bgmClip;
    public bool loopBgm = true;

    bool spawned = false;
    BossBase boss;

    void Update()
    {
        if (!spawned && Input.GetKeyDown(KeyCode.Q))
            SpawnBossOnce();
    }

    void SpawnBossOnce()
    {
        spawned = true;

        // 0) 프리체크
        if (!bossPrefab)
        {
            Debug.LogError("[BossSeq] bossPrefab 미지정");
            return;
        }

        // 1) 보스 스폰
        var go = Instantiate(bossPrefab, bossWorldPos, Quaternion.identity);
        boss = go ? go.GetComponent<BossBase>() : null;
        if (!boss)
        {
            Debug.LogError("[BossSeq] bossPrefab에 BossBase가 없습니다.");
            return;
        }

        // 2) UI 참조 확인(없으면 실패 지점 로그 후 계속 진행하지 않음)
        if (!bossBarRoot || !hpSlider || !hpText || !hpFill)
        {
            Debug.LogError("[BossSeq] 보스바 UI 참조가 비었습니다. bossBarRoot / hpSlider / hpText / hpFill 연결 필요");
            return;
        }

        // 3) 보스바 바인딩
        boss.bossBarRoot = bossBarRoot;
        boss.hpSlider    = hpSlider;
        boss.hpText      = hpText;
        boss.hpFill      = hpFill;
        if (nameText) boss.nameTextTarget = nameText;

        // 4) 등장 연출 + HP 충전
        boss.ShowBarWithCharge(chargeSeconds);

        // 5) 등장 효과
        if (sfxIntro)
            AudioSource.PlayClipAtPoint(sfxIntro,
                Camera.main ? Camera.main.transform.position : transform.position, 1f);
        if (fxIntro)
            Instantiate(fxIntro, boss.transform.position, Quaternion.identity);

        // 6) BGM
        if (bgmClip)
        {
            if (!bgmSource)
            {
                // 없으면 임시 생성해 재생(2D)
                var src = new GameObject("BGM_Temp").AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.loop = loopBgm;
                src.clip = bgmClip;
                src.Play();
                bgmSource = src;
            }
            else
            {
                bgmSource.loop = loopBgm;
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }
        }

        // 7) 종료 콜백
        boss.OnBossDie += OnBossDie;
    }


    void OnBossDie(BossBase b)
    {
        if (bgmSource && bgmSource.isPlaying) bgmSource.Stop();
        boss.OnBossDie -= OnBossDie;
    }
}
