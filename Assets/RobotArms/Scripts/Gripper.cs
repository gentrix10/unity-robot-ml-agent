using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class Gripper : MonoBehaviour
{
    // Gripper의 역할
    // ① 팔 끝을 따라간다   ② 손가락을 여닫는다   ③ 닿았는지 알려준다

    [Header("연결")] public Transform follow; // 팔 끝 (Tip)
    public Transform yawReference; // 받침대 (Base)
    public Transform target; // 옮길 물체 (Object)
    public HingeJoint fingerL;
    public HingeJoint fingerR;

    [Header("쥐는 힘")] public float gripSpeed = 90f; // 여닫는 속도(도/초). 빠르면 물체를 날려버린다
    public float gripForce = 4f; // 쥐는 힘(N·m). 세면 튕겨나간다
    public float gripRange = 65f; // 활짝 벌림 ~ 완전히 오므림 각도
    public float gripDepth = 0.22f; // 손목에서 손가락 사이까지

    [Header("쉬는 자세")]
    // 에피소드마다 손가락을 이 자리로 되돌린다.
    public float fingerOffset = 0.17f; // 손목 중심 ~ 경첩 (좌우)

    public float openAngle = 50f;

    const float ContactMargin = 0.01f; // 표면이 맞닿은 상태를 잡기 위한 여유

    Rigidbody _body, _bodyL, _bodyR;
    BoxCollider[] _boxL, _boxR;
    Collider _targetCollider;
    readonly Collider[] _hits = new Collider[8];

    // 손가락 사이 한가운데. 물체를 가져다 대야 하는 자리다.
    public Vector3 GripCenter =>
        transform.position + transform.forward * gripDepth;

    // 0 = 활짝 벌림, 1 = 완전히 오므림. 물고 있으면 중간에서 멈춘다.
    public float GripValue =>
        (Angle(fingerL, 1f) + Angle(fingerR, -1f)) * 0.5f;

    public bool TouchingL { get; private set; }

    public bool TouchingR { get; private set; }

    //양쪽이 모두 닿았다 = 물고 있다.
    public bool BothTouching => TouchingL && TouchingR;

    // 경첩 설정이 잘못되면 물리 엔진이 NaN을 돌려준다. 관측에 넣기 전에 막는다.
    float Angle(HingeJoint h, float sign)
    {
        if (h == null) return 0f;
        float t = h.angle * sign / Mathf.Max(1f, gripRange);
        return float.IsNaN(t) ? 0f : Mathf.Clamp01(t);
    }

    void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;

        _bodyL = fingerL.GetComponent<Rigidbody>();
        _bodyR = fingerR.GetComponent<Rigidbody>();
        _boxL = fingerL.GetComponentsInChildren<BoxCollider>();
        _boxR = fingerR.GetComponentsInChildren<BoxCollider>();
        
        _targetCollider = target.GetComponent<Collider>();
    }

    void FixedUpdate()
    {
        if (follow == null) return;
        _body.MovePosition(follow.position);
        _body.MoveRotation(AimRotation(follow.position, yawReference.position));

    }

    public static Quaternion AimRotation(Vector3 tip, Vector3 basePos)
    {
        Vector3 flat = tip - basePos;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        return Quaternion.LookRotation(Vector3.down, flat.normalized);
    }

    public void Drive(bool close)
    {
        Motor(fingerL, close ? 1f : -1f);
        Motor(fingerR, close ? -1f : 1f);
    }

    void Motor(HingeJoint h, float direction)
    {
        h.motor = new JointMotor
        {
            targetVelocity = direction * gripSpeed,
            force = gripForce
        };
        h.useMotor = true;
    }

    public void UpdateContacts()
    {
        TouchingL = Overlaps(_boxL);
        TouchingR = Overlaps(_boxR);
    }

    bool Overlaps(BoxCollider[] boxes)
    {
        foreach (var box in boxes)
        {
            var t = box.transform;
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f
                           + Vector3.one * ContactMargin;

            int n = 
                Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), half,
                _hits, t.rotation, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < n; i++)
                if (_hits[i] == _targetCollider) return true;
        }
        return false;
    }

    public void ResetPose()
    {
        transform.SetPositionAndRotation(
            follow.position, 
            AimRotation(follow.position, yawReference.position));

        Restore(_bodyL, -fingerOffset, -openAngle);
        Restore(_bodyR, +fingerOffset, +openAngle);
        
        Drive(false);
        TouchingL = TouchingR = false;
        Physics.SyncTransforms();   // 손으로 옮겼으니 물리 엔진에 알려준다
    }

    void Restore(Rigidbody rb, float sideOffset, float angle)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.transform.SetPositionAndRotation(
            transform.TransformPoint(new Vector3(sideOffset, 0f, 0f)),
            transform.rotation * Quaternion.AngleAxis(angle, Vector3.up));
    }

}
