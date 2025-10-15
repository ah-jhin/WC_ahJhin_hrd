using UnityEngine;

public class ElectricBeam : MonoBehaviour
{
    [Header("Beam Settings")]
    public float speed = 8f;              // 좌↔우 이동 속도
    public int damage = 25;               // 닿을 때 주는 피해
    public float maxLifeTime = 8f;        // 안전 소멸 타이머
    public Vector2 moveDir = Vector2.right; // +x 또는 -x
    public float ignorePlayerTime = 0.25f;  // 스폰 직후 무시 시간

    private float _spawnTime;

    void OnEnable()
    {
        _spawnTime = Time.time;
    }

    void Update()
    {
        // 이동
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

        // 화면 밖 제거
        var cam = Camera.main;
        if (cam)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            if (vp.x < -0.2f || vp.x > 1.2f) Destroy(gameObject);
        }

        // 수명 초과 시 제거
        if (Time.time - _spawnTime > maxLifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 스폰 직후 잠깐 무시
        if (Time.time - _spawnTime < ignorePlayerTime) return;
        if (!other.CompareTag("Player")) return;

        // 대쉬 중이면 통과
        var pm = other.GetComponent<PlayerMovement>();
        if (pm != null && pm.IsDashing) return;

        // 피격 처리
        var hp = other.GetComponent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage, false, 1f);
    }
}
