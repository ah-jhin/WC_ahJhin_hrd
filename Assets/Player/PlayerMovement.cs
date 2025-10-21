using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerMovement 스크(전체 교체본)
/// - 방향키 좌우 이동 + 점프(버퍼/가변 높이)
/// - 회피: 위/아래/좌/우, 트레일/쿨타임 바
/// - FirePoint 좌우 반전 유지
/// - Pain 트리거는 PlayerHealth로 전달만 함
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 점프")]
    public float moveSpeed = 5f;          // 좌우 이동 속도
    public float jumpForce = 10f;         // 기본 점프 힘
    public float highJumpForce = 16f;     // 강화 점프 힘
    public float jumpTime = 0.3f;         // 점프 키 유지 시 추가 가속
    public float jumpBufferTime = 0.15f;  // 점프 입력 버퍼
    public float fallMultiplier = 0.5f;   // 물속 낙하 감속

    [Header("Dash(회피)")]
    public KeyCode dashKey = KeyCode.C;   // 회피 키
    public float dashCooldown = 1.0f;     // 회피 쿨타임(초)
    public float dashDuration = 0.12f;    // 회피 잠금 시간
    public float dashPowerH = 14f;        // 좌/우 대시 힘
    public float dashPowerV = 4f;         // 위 대시 힘
    public float dashPowerDown = 4f;      // 아래 대시 힘

    [Header("Dash 시각화")]
    public TrailRenderer dashTrail;           // 회피 트레일(선택)
    public Transform cooldownBarRoot;         // 쿨타임 바 루트
    public SpriteRenderer cooldownBarFill;    // 바 채움 스프라이트

    [Header("사운드(SFX)")]
    public AudioSource sfx;               // 효과음 소스(선택)
    public AudioClip sfxDash;             // 회피 SFX(선택)

    [Header("상태")]
    public bool isGrounded = false;       // 바닥 접지
    public bool isInWater = false;        // 물 영역 여부

    [Header("총구")]
    public Transform firePoint;           // FirePoint

    [Header("Pain 전달 값(인스펙터 조절)")]
    public int painMin = 5;               // 난수 최소
    public int painMax = 10;              // 난수 최대
    public float painKnockback = 10f;     // 넉백 힘

    // --- 내부 ---
    Rigidbody2D rb;
    SpriteRenderer sr;
    float moveInput = 0f;                 // -1,0,1
    bool isJumping = false;
    bool hasAirJumped = false;
    bool hasExtraJump = false;
    bool useHighJump = false;
    float jumpBufferCounter = 0f;
    float jumpTimeCounter = 0f;
    bool isDashing = false;
    float nextDashTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>(); // 자식 SR도 허용
        if (!dashTrail) dashTrail = GetComponentInChildren<TrailRenderer>();
        if (cooldownBarRoot) cooldownBarRoot.gameObject.SetActive(false);
        if (dashTrail) { dashTrail.emitting = false; dashTrail.Clear(); }
    }

    void Update()
    {
        // 1) 방향키 입력만 사용(A/D 비활성)
        float h = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))  h = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) h =  1f;
        moveInput = h;

        // 2) 시선 Flip
        if (moveInput > 0) sr.flipX = false;
        else if (moveInput < 0) sr.flipX = true;

        // 3) 점프 버퍼(X 키)
        if (Input.GetKeyDown(KeyCode.X)) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0)
        {
            if (isGrounded || !hasAirJumped || hasExtraJump || isInWater)
            {
                float force = useHighJump ? highJumpForce : jumpForce;
#if UNITY_600_0_OR_NEWER
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
#else
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
#endif
                isJumping = true;
                jumpTimeCounter = jumpTime;
                jumpBufferCounter = 0f;

                if (!isGrounded && !isInWater)
                {
                    if (hasExtraJump) { hasExtraJump = false; useHighJump = false; }
                    hasAirJumped = true;
                }
            }
        }

        // 4) 점프 가변 높이
        if (Input.GetKey(KeyCode.X) && isJumping && jumpTimeCounter > 0f)
        {
#if UNITY_600_0_OR_NEWER
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, useHighJump ? highJumpForce : jumpForce);
#else
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, useHighJump ? highJumpForce : jumpForce);
#endif
            jumpTimeCounter -= Time.deltaTime;
        }
        if (Input.GetKeyUp(KeyCode.X)) isJumping = false;

        // 5) 회피 입력
        HandleDashInput();

        // 6) FirePoint 좌우 오프셋 유지
        if (firePoint)
        {
            float ox = 0.3f;
            firePoint.localPosition = new Vector3(sr.flipX ? -ox : +ox, firePoint.localPosition.y, 0);
        }

        // 7) 쿨타임 바
        UpdateDashCooldownUI();
    }

    void FixedUpdate()
    {
        if (!isDashing)
        {
#if UNITY_600_0_OR_NEWER
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
#else
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
#endif
        }

        if (isInWater)
        {
#if UNITY_600_0_OR_NEWER
            if (rb.linearVelocity.y < 0) rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * fallMultiplier);
#else
            if (rb.linearVelocity.y < 0) rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * fallMultiplier);
#endif
        }
    }

    // 회피 처리
    void HandleDashInput()
    {
        if (!Input.GetKeyDown(dashKey) || Time.time < nextDashTime || isDashing) return;

        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))  h = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) h =  1f;
        if (Input.GetKey(KeyCode.UpArrow))    v =  1f;
        if (Input.GetKey(KeyCode.DownArrow))  v = -1f;

        Vector2 dir;
        if (Mathf.Abs(v) > 0.1f) dir = new Vector2(0, Mathf.Sign(v));
        else if (Mathf.Abs(h) > 0.1f) dir = new Vector2(Mathf.Sign(h), 0);
        else dir = new Vector2(sr.flipX ? -1 : 1, 0);

        float power = (dir.y > 0.1f) ? dashPowerV : (dir.y < -0.1f ? dashPowerDown : dashPowerH);
        StartCoroutine(DashRoutine(dir.normalized, power));
    }

    System.Collections.IEnumerator DashRoutine(Vector2 dir, float power)
    {
        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        if (sfx && sfxDash) sfx.PlayOneShot(sfxDash, AudioBus.SFX);
        if (dashTrail) dashTrail.emitting = true;

#if UNITY_600_0_OR_NEWER
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y);
#else
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y);
#endif
        rb.AddForce(dir * power, ForceMode2D.Impulse);

        float end = Time.time + dashDuration;
        while (Time.time < end) yield return null;

        if (dashTrail) dashTrail.emitting = false;
        isDashing = false;
    }

    void UpdateDashCooldownUI()
    {
        if (!cooldownBarRoot || !cooldownBarFill) return;
        float remain = Mathf.Clamp01((nextDashTime - Time.time) / dashCooldown);

        if (remain <= 0f) { cooldownBarRoot.gameObject.SetActive(false); return; }

        cooldownBarRoot.gameObject.SetActive(true);
        var s = cooldownBarFill.transform.localScale;
        s.x = remain;                      // 1→0 형태면 1 - remain 쓰면 됨
        cooldownBarFill.transform.localScale = s;
    }

    // 접지/물/오브
    void OnCollisionEnter2D(Collision2D c)
    {
        if (c.contactCount > 0 && c.GetContact(0).normal.y > 0.7f)
        { isGrounded = true; hasAirJumped = false; isJumping = false; }
    }
    void OnCollisionExit2D(Collision2D c) { isGrounded = false; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Water")) isInWater = true;

        if (other.CompareTag("BlueJumpOrb"))
        { hasExtraJump = true; useHighJump = false; other.gameObject.SetActive(false); Invoke(nameof(ReactivateOrb), 3f); }

        if (other.CompareTag("RedJumpOrb"))
        { hasExtraJump = true; useHighJump = true;  other.gameObject.SetActive(false); Invoke(nameof(ReactivateOrb), 3f); }

        // pain은 PlayerHealth로 위임
        if (other.CompareTag("pain"))
            GetComponent<PlayerHealth>()?.ApplyPain(painMin, painMax, painKnockback, other.transform.position);
    }
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("pain"))
            GetComponent<PlayerHealth>()?.ApplyPain(painMin, painMax, painKnockback, other.transform.position);
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Water")) isInWater = false;
    }

    void ReactivateOrb() { /* 오브 재활성 자리 */ }

    // 조준 방향
    public Vector2 GetAimDir()
    {
        if (Input.GetKey(KeyCode.UpArrow)) return Vector2.up;
        if (!isGrounded && Input.GetKey(KeyCode.DownArrow)) return Vector2.down;
        return new Vector2(sr.flipX ? -1f : 1f, 0f);
    }

    public bool IsDashing => isDashing;
}
