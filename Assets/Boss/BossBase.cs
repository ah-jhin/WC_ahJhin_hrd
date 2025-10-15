// Assets/Boss/BossBase.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // ★ 이벤트용

/// <summary>
/// 모든 보스 공통 베이스
/// - 체력/피해/HP UI 처리 단일화
/// - IDamageable 구현으로 Bullet과 직접 연동
/// - DamageNumberPool은 런타임에 한 번 찾아 캐시(싱글턴 필요 없음)
/// - OnHpChanged / OnBossDie 이벤트 제공(보스 전환·HUD 연동)
/// </summary>
public class BossBase : MonoBehaviour, IDamageable
{
    [Header("Boss Stats")]
    [Tooltip("보스 최대 체력(프리팹 기본값, 전환 컨트롤러에서 덮어쓸 수 있음)")]
    public int maxHP = 100;                 // 보스 최대 체력(인스펙터에서 설정)
    protected int currentHP;                // 현재 체력(내부 관리)

    [Header("UI (Shared / Rebind at runtime)")]
    [Tooltip("공유 HP 슬라이더(전환 컨트롤러가 런타임에 꽂아주면 프리팹 간 재사용 가능)")]
    public Slider hpSlider;                 // 보스 체력바
    [Tooltip("예: 1000 / 2000 같은 표기용")]
    public TextMeshProUGUI hpText;          // "현재/최대" 표기
    public int GetCurrentHP() => currentHP; // 외부(HUD 등)에서 읽기용

    [Header("Death")]
    [Tooltip("사망 후 Destroy 지연(연출/VFX 재생 후 제거하고 싶을 때 사용)")]
    public float deathDelay = 0f;

    // ▼ 이벤트: HUD/보스 전환 컨트롤러에서 구독
    public event Action<int, int> OnHpChanged;    // (current, max)
    public event Action<BossBase> OnBossDie;

    // ▼ 데미지 숫자 풀 캐시(씬에 하나만 있으면 됨)
    private DamageNumberPool _dmgPool;      // 씬에서 한 번 찾아서 저장

    protected virtual void Start()
    {
        // currentHP 초기화는 Start에서 수행(Instantiate 직후 컨트롤러가 maxHP를 덮어써도 반영됨)
        currentHP = maxHP;
        UpdateUI();

        // 데미지 숫자 풀 자동 탐색(없어도 동작은 함)
#if UNITY_2023_1_OR_NEWER
        _dmgPool = FindFirstObjectByType<DamageNumberPool>();
#else
#pragma warning disable CS0618
        _dmgPool = FindObjectOfType<DamageNumberPool>();
#pragma warning restore CS0618
#endif
    }

    /// <summary>
    /// 외부에서 UI를 공유/재주입할 때 사용(전환 컨트롤러에서 호출)
    /// </summary>
    public void SetUI(Slider slider, TextMeshProUGUI text)
    {
        hpSlider = slider;
        hpText = text;
        UpdateUI(); // 즉시 반영
    }

    /// <summary>
    /// 외부에서 체력값(최대/현재)을 초기화하고 싶을 때(예: 보스 체인 전환 직후)
    /// </summary>
    public void InitHP(int newMaxHP, int? newCurrentHP = null, bool clamp = true)
    {
        maxHP = Mathf.Max(1, newMaxHP);
        currentHP = newCurrentHP.HasValue ? newCurrentHP.Value : maxHP;
        if (clamp) currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateUI();
        OnHpChanged?.Invoke(currentHP, maxHP);
    }

    /// <summary>
    /// IDamageable 구현: 어떤 탄/공격이든 여기로 수렴
    /// amount: 기본 데미지, weak: 약점 여부, weakMultiplier: 약점 배율
    /// </summary>
    public void TakeDamage(int amount, bool weak, float weakMultiplier)
    {
        // 1) 최종 데미지 계산(약점 보정)
        int final = weak ? Mathf.RoundToInt(amount * weakMultiplier) : amount;
        final = Mathf.Max(0, final);

        // 2) 체력 감소 및 UI 반영
        currentHP = Mathf.Max(0, currentHP - final);
        UpdateUI();
        OnHpChanged?.Invoke(currentHP, maxHP); // ★ HUD/컨트롤러 통지

        // 3) 데미지 숫자 표시(풀이 있으면 사용)
        //    - 월드 스페이스 캔버스(dmgPool)에서 숫자를 스폰
        if (_dmgPool != null)
        {
            Vector3 worldPos = transform.position + Vector3.up * 0.6f; // 머리 위
            Color col = weak ? Color.yellow : Color.white;             // 약점=노랑
            _dmgPool.Spawn(worldPos, final, col);
        }

        // 4) 사망 처리
        if (currentHP == 0) Die();
    }

    /// <summary>사망 공통 처리(파생 보스는 필요하면 override)</summary>
    protected virtual void Die()
    {
        Debug.Log($"[BossBase] {name} 사망");

        // 전환 컨트롤러/퀘스트 매니저 등에 알림
        OnBossDie?.Invoke(this);

        // 지연 제거(연출용)
        if (deathDelay > 0f)
            Destroy(gameObject, deathDelay);
        else
            Destroy(gameObject);
    }

    /// <summary>보스 HP UI 업데이트</summary>
    protected void UpdateUI()
    {
        if (hpSlider)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }
        if (hpText) hpText.text = $"{currentHP} / {maxHP}";
    }
}
