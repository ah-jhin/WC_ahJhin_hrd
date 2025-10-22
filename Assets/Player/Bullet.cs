using UnityEngine;

/// <summary>
/// 발사체(2D)
/// - 무기에서 SetLifetime → Inject 순으로 호출해야 함
/// - Inject 전에는 충돌을 비활성(armed=false)하여 잘못된 피격을 막음
/// - 약점은 "보너스 더하기"로 처리
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [Header("수명(초) — SO에서 덮어씀")]
    public float lifetime = 2.5f;         // 기본값. 무기에서 SetLifetime으로 재설정

    [Header("디폴트 피해(비상용)")]
    public int defaultDamage = 1;         // Inject 누락 시 사용할 최소값

    // 내부 상태
    int damage;                            // 기본 피해
    int weakBonus;                         // 약점 보너스(+)
    bool armed = false;                    // Inject 호출 전까지 비무장
    bool warned = false;                   // 경고 1회용

    Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col) col.enabled = false;     // ■ Inject 전 충돌 금지
    }

    void OnEnable()
    {
        // 프리팹 기본값으로도 동작. 무기에서 SetLifetime을 다시 호출하면 타이머 갱신됨.
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    /// <summary>
    /// 무기에서 수명을 재설정한다.
    /// </summary>
    public void SetLifetime(float seconds)
    {
        lifetime = seconds;
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    /// <summary>
    /// 무기에서 피해/약점 보너스를 주입한다. 호출 시 충돌을 활성화(무장)한다.
    /// </summary>
    public void Inject(int baseDamage, int weakAdd)
    {
        damage = Mathf.Max(0, baseDamage);
        weakBonus = Mathf.Max(0, weakAdd);
        Arm();                              // ■ 무장 + 타이머 보강
    }

    void Arm()
    {
        armed = true;
        if (col) col.enabled = true;        // ■ 이제 충돌 허용
        // 안전: 수명 타이머 재보장
        CancelInvoke(nameof(Die));
        if (lifetime > 0f) Invoke(nameof(Die), lifetime);
    }

    void Die()
    {
        if (this) Destroy(gameObject);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed)
        {
            if (!warned) { Debug.LogWarning("[Bullet] Inject()가 호출되기 전에 충돌했습니다."); warned = true; }
            return;
        }

        var target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            bool isWeak = other.CompareTag("WeakPoint"); // ★ 약점은 태그로만 판정
            target.TakeDamage(damage, isWeak, weakBonus);
        }

        Die();
    }
}