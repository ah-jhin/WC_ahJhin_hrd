using UnityEngine;

/// <summary>
/// 오브젝트와 '접촉' 시 플레이어에게 피해를 주는 함정/투사체용 스크립트
/// - 태그: Player, block 사용
/// - 이 스크립트의 '무적 시간'이 PlayerHealth 무적보다 우선한다(오버로드 호출).
/// - 트리거/충돌 둘 다 지원(OnTriggerEnter2D / OnCollisionEnter2D)
/// </summary>
[AddComponentMenu("Game/Pain")]
[DisallowMultipleComponent]
public class pain : MonoBehaviour
{
    [Header("수치")]
    [Tooltip("최소 피해값")]
    public int minDamage = 5;
    [Tooltip("최대 피해값")]
    public int maxDamage = 10;
    [Tooltip("넉백 힘(좌/우는 랜덤, 위로 50% 가산)")]
    public float knockback = 10f;
    [Tooltip("같은 함정에 재접촉 시 이 시간 간격으로만 피해 적용(초)")]
    public float localInvincible = 0.4f;

    [Header("접촉(플레이어)")]
    [Tooltip("접촉 시 즉시 소멸할지 여부")]
    public bool destroyOnTouch = false;
    [Tooltip("접촉 시 생성될 일반 프리팹(데칼 등)")]
    public GameObject touchPrefab;
    [Tooltip("접촉 시 VFX 프리팹")]
    public GameObject touchVFX;
    [Tooltip("접촉 시 SFX")]
    public AudioClip touchSFX;

    [Header("물리")]
    [Tooltip("중력을 사용할지 여부")]
    public bool useGravity = false;
    [Tooltip("중력 값(리지드바디2D의 gravityScale로 반영)")]
    public float gravityScale = 1f;
    [Tooltip("블럭(tag=block)과 충돌 체크할지 여부")]
    public bool checkBlockCollision = true;
    [Tooltip("블럭과 충돌 시 소멸할지 여부")]
    public bool destroyOnBlock = true;
    [Tooltip("블럭 충돌 시 생성할 일반 프리팹")]
    public GameObject blockHitPrefab;
    [Tooltip("블럭 충돌 시 VFX")]
    public GameObject blockHitVFX;
    [Tooltip("블럭 충돌 시 SFX")]
    public AudioClip blockHitSFX;

    [Header("소멸(타이머)")]
    [Tooltip("시간 경과로 자동 소멸할지 여부")]
    public bool autoDespawn = false;
    [Tooltip("생성 후 n초 뒤 자동 소멸")]
    public float despawnAfter = 10f;
    [Tooltip("소멸 시 생성할 일반 프리팹")]
    public GameObject despawnPrefab;
    [Tooltip("소멸 시 VFX")]
    public GameObject despawnVFX;
    [Tooltip("소멸 시 SFX")]
    public AudioClip despawnSFX;

    [Header("태그 설정")]
    [Tooltip("플레이어 태그명")]
    public string playerTag = "Player";
    [Tooltip("블럭 태그명")]
    public string blockTag = "block";

    // 내부 구성요소
    Rigidbody2D _rb;
    Collider2D _col;
    AudioSource _audio;

    // 이 함정 자체의 쿨다운(한 플레이어 게임 가정)
    float _nextTouchTime = 0f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        // 중력 설정
        if (useGravity)
        {
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = gravityScale;
        }
        else if (_rb != null)
        {
            _rb.gravityScale = 0f;
        }

        // 자동 소멸 타이머
        if (autoDespawn)
            Invoke(nameof(DoDespawn), Mathf.Max(0.01f, despawnAfter));
    }

    // --------- 트리거 방식 ----------
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            TryDamagePlayer(other.GetComponent<PlayerHealth>());
        else if (checkBlockCollision && other.CompareTag(blockTag))
            OnBlockHit();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            TryDamagePlayer(other.GetComponent<PlayerHealth>());
    }

    // --------- 충돌 방식 ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(playerTag))
            TryDamagePlayer(collision.collider.GetComponent<PlayerHealth>());
        else if (checkBlockCollision && collision.collider.CompareTag(blockTag))
            OnBlockHit();
    }

    /// <summary>
    /// 플레이어에게 피해 시도. 이 스크립트의 쿨다운(localInvincible) 기준으로만 제한.
    /// PlayerHealth 무적시간은 '무시'하도록 오버로드를 호출한다.
    /// </summary>
    void TryDamagePlayer(PlayerHealth ph)
    {
        if (ph == null) return;

        // 이 함정의 로컬 쿨다운
        if (Time.time < _nextTouchTime) return;
        _nextTouchTime = Time.time + Mathf.Max(0f, localInvincible);

        // 피해 적용: PlayerHealth 무적을 '무시'하는 오버로드 사용
        ph.ApplyPain(minDamage, maxDamage, knockback, transform.position, true);

        // 접촉 연출
        SpawnAll(touchPrefab, touchVFX, touchSFX);

        if (destroyOnTouch)
            DoDespawn();
    }

    /// <summary>블럭(tag=block) 충돌 처리</summary>
    void OnBlockHit()
    {
        SpawnAll(blockHitPrefab, blockHitVFX, blockHitSFX);
        if (destroyOnBlock)
            DoDespawn();
    }

    /// <summary>소멸 처리 + 연출</summary>
    void DoDespawn()
    {
        SpawnAll(despawnPrefab, despawnVFX, despawnSFX);
        Destroy(gameObject);
    }

    /// <summary>프리팹, VFX, SFX를 한 번에 처리</summary>
    void SpawnAll(GameObject prefab, GameObject vfx, AudioClip sfx)
    {
        if (prefab) Instantiate(prefab, transform.position, Quaternion.identity);
        if (vfx) Instantiate(vfx, transform.position, Quaternion.identity);
        if (sfx && _audio) _audio.PlayOneShot(sfx);
    }
}
