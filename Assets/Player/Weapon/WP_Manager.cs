using UnityEngine;

/// <summary>
/// WP_Manager 스크(교정본)
/// - 슬롯0=권총, 1~2=보급
/// - Z 또는 Fire1로 발사, A로 순환, 1~3 직접 선택
/// - 활성 무기의 public void Shoot()만 있으면 동작
/// - HUD 갱신은 무기 교체/시작 시점에만 호출
/// </summary>
public class WP_Manager : MonoBehaviour
{
    [Header("무기 슬롯 (0=권총, 1~2=보급)")]
    public GameObject[] weaponSlots = new GameObject[3];

    [Header("입력")]
    public KeyCode nextKey = KeyCode.A;    // 무기 교체
    public KeyCode shootKey = KeyCode.Z;   // 발사
    public UIHUD hud;                      // HUD 참조(선택)

    [Header("발사 공통 쿨타임")]
    public float fireRate = 6f;            // 초당 발사수(예: 6 = 0.166s)
    private float _nextFireTime;           // 다음 발사 가능 시각

    private int _cur = 0;                  // 현재 슬롯 인덱스(0~2)

    [Header("권총 프리팹")]
    public GameObject pistolPrefab;        // 슬롯0이 비었을 때 런타임 스폰

    void Start()
    {
        // 슬롯0이 비면 권총 프리팹을 자식으로 생성
        if (weaponSlots[0] == null && pistolPrefab != null)
        {
            weaponSlots[0] = Instantiate(pistolPrefab, transform);
            weaponSlots[0].name = "Pistol(runtime)";
            weaponSlots[0].SetActive(true);
        }

        // 현재 슬롯만 활성화(초기값=0)
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] != null) weaponSlots[i].SetActive(i == 0);

        _cur = 0;
        _nextFireTime = Time.time;

        // ★ HUD 초기 갱신은 Start에서 한 번만
        RefreshHUDWeapon();
    }

    void Update()
    {
        HandleSwapInput();   // 무기 교체 입력
        HandleFireInput();   // 발사 입력
    }

    /// <summary>무기 교체 입력(A, 1~3)</summary>
    void HandleSwapInput()
    {
        if (Input.GetKeyDown(nextKey)) SwapNext();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwapTo(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwapTo(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwapTo(2);
    }

    /// <summary>발사 입력 처리(Fire1 또는 Z)</summary>
    void HandleFireInput()
    {
        bool firePressed = Input.GetButton("Fire1") || Input.GetKey(shootKey);
        if (!firePressed) return;
        if (Time.time < _nextFireTime) return;

        var active = GetActiveWeapon();
        if (active == null) return;

        // 활성 무기의 Shoot() 호출(없어도 에러 없음)
        active.SendMessage("Shoot", SendMessageOptions.DontRequireReceiver);

        _nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
    }

    /// <summary>다음 무기로 순환(빈 슬롯은 건너뜀)</summary>
    public void SwapNext()
    {
        int start = _cur;
        do { _cur = (_cur + 1) % weaponSlots.Length; }
        while (weaponSlots[_cur] == null && _cur != start);

        ActivateCurrent();   // ★ 여기서 HUD도 갱신됨
    }

    /// <summary>특정 인덱스로 교체</summary>
    public void SwapTo(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        if (weaponSlots[index] == null) return;
        _cur = index;
        ActivateCurrent();   // ★ 여기서 HUD도 갱신됨
    }

    /// <summary>현재 슬롯만 활성화 + HUD 갱신</summary>
    void ActivateCurrent()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
            if (weaponSlots[i] != null) weaponSlots[i].SetActive(i == _cur);

        // 교체 직후 바로 발사 가능
        _nextFireTime = Mathf.Min(_nextFireTime, Time.time);

        Debug.Log($"[WP_Manager] 현재 무기 슬롯: {_cur + 1}");

        // ★ 무기 교체 시점에만 HUD 갱신
        RefreshHUDWeapon();
    }

    /// <summary>활성 무기 반환</summary>
    GameObject GetActiveWeapon()
    {
        if (_cur < 0 || _cur >= weaponSlots.Length) return null;
        return weaponSlots[_cur];
    }

    /// <summary>HUD에 무기 정보 반영(IWeaponInfo 구현 무기만)</summary>
    void RefreshHUDWeapon()
    {
        if (!hud) return;
        var go = GetActiveWeapon();
        if (!go) { hud.SetWeapon(null, "", 0, true); return; }

        var w = go.GetComponent<IWeaponInfo>(); // 무기가 구현하면 HUD 표시 가능
        if (w != null) hud.SetWeapon(w.Icon, w.DisplayName, w.Ammo, w.IsInfinite);
    }

    /// <summary>보급 무기 지급: 1→2 채우고, 가득 차면 2번 교체</summary>
    public void AddWeapon(GameObject newWeaponPrefab)
    {
        if (newWeaponPrefab == null) return;

        if (weaponSlots[1] == null)
        {
            weaponSlots[1] = Instantiate(newWeaponPrefab, transform);
            weaponSlots[1].SetActive(false);
            _cur = 1; ActivateCurrent();
            Debug.Log($"[WP_Manager] 무기 획득(슬롯2): {newWeaponPrefab.name}");
            return;
        }

        if (weaponSlots[2] == null)
        {
            weaponSlots[2] = Instantiate(newWeaponPrefab, transform);
            weaponSlots[2].SetActive(false);
            _cur = 2; ActivateCurrent();
            Debug.Log($"[WP_Manager] 무기 획득(슬롯3): {newWeaponPrefab.name}");
            return;
        }

        // 두 슬롯이 모두 찼으면 2번 교체
        Destroy(weaponSlots[2]);
        weaponSlots[2] = Instantiate(newWeaponPrefab, transform);
        weaponSlots[2].SetActive(false);
        _cur = 2; ActivateCurrent();
        Debug.Log($"[WP_Manager] 무기 교체(슬롯3): {newWeaponPrefab.name}");
    }
}
