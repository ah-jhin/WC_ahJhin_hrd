using UnityEngine;

#region 상태 열거형
// 플레이어 상태 열거형 정의
public enum State { Idle, Move, Jump, LookUp, JumpLookUp, JumpLookDown }
#endregion

[RequireComponent(typeof(AudioSource))] // ← 오디오소스가 없으면 자동 추가 요구
public class PlayerState : MonoBehaviour
{
    [Header("애니메이션 클립(선택)")]
    public AnimationClip idleClip;        // 대기
    public AnimationClip moveClip;        // 이동(루프)
    public AnimationClip jumpClip;        // 점프
    public AnimationClip lookUpClip;      // 위보기
    public AnimationClip jumpLookUpClip;  // 공중 위보기
    public AnimationClip jumpLookDownClip;// 공중 아래보기

    [Header("효과음(SFX, 비어있으면 재생 생략)")]
    [SerializeField] private AudioSource sfx;   // 재생용 AudioSource
    public AudioClip moveSFX;                   // 이동 발소리
    public AudioClip jumpSFX;                   // 1단 점프
    public AudioClip doubleJumpSFX;             // 2단 점프
    public AudioClip swapWeaponSFX;             // 무기 교체
    public AudioClip dropWeaponSFX;             // 무기 버리기
    public AudioClip hitSFX;                    // 피격

    // 내부 컴포넌트(선택 사용)
    private Animator animator;                  // Animator 사용 시
    private Animation legacyAnim;               // Legacy Animation 사용 시
    private SpriteRenderer spriteRenderer;      // 스프라이트 직접 제어 시

    // 상태 및 발소리 타이머
    private State currentState = State.Idle;    // 현재 상태
    private float footstepTimer = 0f;           // 발소리 간격 타이머
    [SerializeField] private float footstepInterval = 0.5f; // 발소리 주기(초)

    #region Unity 생명주기
    private void Awake()
    {
        // AudioSource 자동 확보: 인스펙터 비어 있으면 GetComponent, 그래도 없으면 AddComponent
        if (!sfx) sfx = GetComponent<AudioSource>();
        if (!sfx) sfx = gameObject.AddComponent<AudioSource>();

        // 발소리는 이동 시작 즉시 한 번 재생되도록 타이머 초기화
        footstepTimer = footstepInterval;
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        legacyAnim = GetComponent<Animation>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Legacy Animation 사용 시 클립 등록
        if (legacyAnim != null)
        {
            if (idleClip) legacyAnim.AddClip(idleClip, "Idle");
            if (moveClip) { moveClip.wrapMode = WrapMode.Loop; legacyAnim.AddClip(moveClip, "Move"); }
            if (jumpClip) legacyAnim.AddClip(jumpClip, "Jump");
            if (lookUpClip) legacyAnim.AddClip(lookUpClip, "LookUp");
            if (jumpLookUpClip) legacyAnim.AddClip(jumpLookUpClip, "JumpLookUp");
            if (jumpLookDownClip) legacyAnim.AddClip(jumpLookDownClip, "JumpLookDown");
        }
    }

    private void Update()
    {
        // TODO: 실제 바닥 체크로 교체할 것 (예: 캐릭터 컨트롤러/레이캐스트)
        bool isGrounded = true; // ← 임시값

        // 입력 읽기
        bool pressLeftRight = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;
        bool pressUp = Input.GetKey(KeyCode.UpArrow);
        bool pressDown = Input.GetKey(KeyCode.DownArrow);

        // 상태 판정
        State newState = currentState;
        if (!isGrounded)
        {
            if (pressUp) newState = State.JumpLookUp;
            else if (pressDown) newState = State.JumpLookDown;
            else newState = State.Jump;
        }
        else
        {
            if (pressUp) newState = State.LookUp;
            else if (pressLeftRight) newState = State.Move;
            else newState = State.Idle;
        }

        // 상태 변경 시 애니메이션 반영
        if (newState != currentState)
        {
            currentState = newState;
            if (animator) animator.Play(currentState.ToString());     // Animator 사용 시
            if (legacyAnim) legacyAnim.Play(currentState.ToString()); // Legacy 사용 시
        }

        // 이동 발소리: SFX가 설정된 경우에만 재생 시도
        if (currentState == State.Move && isGrounded && moveSFX)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                PlaySFX(moveSFX);  // ← moveSFX가 null이면 내부에서 자동 무시
                footstepTimer = 0f;
            }
        }
        else
        {
            // 이동이 아니면 다음 이동 시 즉시 재생되도록 채워둠
            footstepTimer = footstepInterval;
        }

        // 단발성 SFX: 각각 할당된 경우에만 호출
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded) { if (jumpSFX) PlaySFX(jumpSFX); }
            else { if (doubleJumpSFX) PlaySFX(doubleJumpSFX); }
        }
        if (Input.GetKeyDown(KeyCode.Q))        { if (swapWeaponSFX) PlaySFX(swapWeaponSFX); }
        if (Input.GetKeyDown(KeyCode.F))        { if (dropWeaponSFX) PlaySFX(dropWeaponSFX); }
        // 피격 SFX는 체력 스크립트 등에서: if (hitSFX) state.PlaySFX(hitSFX);
    }
    #endregion

    #region SFX 유틸리티
    /// <summary>
    /// SFX 재생 헬퍼.
    /// - clip 또는 sfx가 없으면 조용히 무시한다. (경고 로그 출력 안함)
    /// </summary>
    private void PlaySFX(AudioClip clip)
    {
        // null 방어: 아무 것도 하지 않음
        if (!clip || !sfx) return;

        // 단발 재생
        sfx.PlayOneShot(clip);
    }
    #endregion
}
