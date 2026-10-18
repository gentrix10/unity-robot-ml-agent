using System;
using System.Collections.Generic;
using UnityEngine;





// 로봇 팔의 "몸"을 담당한다.
public class RobotArmController : MonoBehaviour
{
    [System.Serializable]
    // 관절의 하나의 설정과 상태 등을 담고 있는 클래스 객체
    public class Joint
    {
        // 객체의 이름
        public string label = "관절"; // 화면에 표시할 이름. 예: 어깨, 팔꿈치, 좌우 회전 
        public Transform pivot; // 회전만 담당하는 오브젝트
        public Vector3 axis = Vector3.left; // 회전축. 회전 방향
        public float minAngle = -90f; // 이 관절이 돌 수 있는 최소 각도(도).
        public float maxAngle = 90f; // "이 관절이 돌 수 있는 최대 각도(도).
        public float maxSpeed = 120f; // 초당 최대 몇 도까지 돌 수 있는가
        public float startAngle = 0f; // 에피소드가 시작될 때 돌아갈 초기 각도
        
        public float Angle { get; private set; } // 지금 각도(도). 이 값이 팔의 진짜 상태다
        public float Velocity { get; private set; } // 지금 각속도(도/초). 관측에 쓴다.

        public void Drive(float normalizedSpeed, float deltaTime)
        {
            // 입력을 "각도"가 아니라 "속도"로 받아서 조금씩 더해 나간다.
            // 그래야 한 프레임에 팔이 순간이동하듯 튀지 않는다.
            float beforeAngle = Angle;
            float deltaAngle = Mathf.Clamp(normalizedSpeed, -1f, 1f) * maxSpeed *  deltaTime;
            
            Angle = Mathf.Clamp(Angle + deltaAngle, minAngle, maxAngle);
            Velocity = deltaTime > 0f ? (Angle - beforeAngle) / deltaTime : 0f;
            
            Apply();
        }

        public void ResetTo(float angle)
        {
            Angle = Mathf.Clamp(angle, minAngle, maxAngle);
            Velocity = 0f;
            Apply();
        }
        
        
        // 해당 모터 실제로 회전을 적용 함수
        void Apply()
        {
            if(pivot != null) 
                pivot.localRotation = Quaternion.AngleAxis(Angle, axis);
        }
    }
    
    // 관절 객체 담은 정보
    // 어깨부터 순서대로 넣는다. 관절 개수 = DOF = 액션 개수.
    public Joint[] joints = new Joint[0];
    
    // 오브젝트가 배치되면 호출되는 최초 함수
    void Awake()
    {
        ResetPose();
    }

    /// 모든 관절을 시작 각도로 되돌린다.
    public void ResetPose()
    {
        foreach (var j in joints)
        {
            j.ResetTo(j.startAngle);
        }
    }

    public void Drive(int index, float normalizedSpeed, float deltaTime)
    {
        float beforeAngle = joints[index].Angle;
        joints[index].Drive(normalizedSpeed, deltaTime);

        if (IsBlocked())
        {
            Debug.Log("BLCOKED");
            // 이번 움직임 때문에 뚫렸다 → 되돌린다.
            // ResetTo는 각속도도 0으로 만든다. 벽에 닿아 멈춘 것처럼 보인다.
            joints[index].ResetTo(beforeAngle);
        }
    }
    

    void FixedUpdate()
    {
        if (RobotArmInput.ResetPressed) ResetPose();

        for (int i = 0; i < joints.Length; i++)
        {
            Drive(i, RobotArmInput.Joint(i), Time.fixedDeltaTime);
        }
    }
    
    // 물체를 집는 지점
    public Transform tip;


    // 바닥(테이블 윗면) 높이. 받침대 기준 y 좌표.
    public float floorHeight = 0f;

    // 관절들에 대한 transform 정보 ( 위치 크기 각동 가져올 수 있는 정보 )
    Transform[] _chain;
    Transform[] Chain
    {
        get
        {
            if (_chain != null && _chain.Length > 0) return _chain;

            var list = new List<Transform>();
            for (var t = tip; t != null && t != transform; t = t.parent) 
                list.Add(t);
            list.Reverse();
            return _chain = list.ToArray();
        }
    }
    
    // 바닥에서 이만큼 띄운다. 링크의 굵기와 팔 끝 구의 반지름을 감안한 여유.
    public float clearance = 0.07f;
    // 팔 끝에서 이 거리 안쪽은 물체 검사에서 제외
    public float tipExemptDistance = 0.28f;
    // 물체를 감싸는 구의 반지름.
    public float obstacleRadius = 0.16f;
    // 뚫고 지나가면 안 되는 물체들.
    public Transform[] obstacles = new Transform[0];
    
    // 팔 끝에 대한 world 표좌표
    public Vector3 TipPosition => 
        tip != null ? tip.position : transform.position;

    // 지금 자세가 바닥을 뚫거나 물체를 관통하고 있는가
    public bool IsBlocked()
    {
        var chain = Chain;
        float minY = floorHeight + clearance;
        Vector3 tipPos = TipPosition;
        
        // 링크 하나를 몇 점으로 쪼개서 검사할지
        int samplesPerLink = 6; 
        
        for (int seg = 0; seg < chain.Length - 1; seg++)
        {
            Vector3 a = chain[seg].position;
            Vector3 b = chain[seg + 1].position;

            for (int s = 0; s <= samplesPerLink; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, s / (float)samplesPerLink);
                
                // 바닥 검사
                if (ToLocal(p).y < minY) return true;
                
                // 물체 검사 — 팔 끝 근처는 제외한다.
                // 그렇지 않으면 물체를 집으러 다가가는 것 자체가 막힌다.
                if (Vector3.Distance(p, tipPos) <= tipExemptDistance) continue;

                foreach (var o in obstacles)
                {
                    if (o == null) continue;
                    if (Vector3.Distance(p, o.position) < obstacleRadius) return true;
                }
            }
        }
        
        return false;
    }
    public Vector3 ToLocal(Vector3 worldPosition) =>
        transform.InverseTransformPoint(worldPosition);
    
    public Vector2 spawnDistanceRange = new Vector2(1.2f, 2.4f);
    public Vector2 spawnYawRange = new Vector2(-60f, 60f);
    public float spawnHeight = 0.1f;
    
    // 거리와 좌우 각도로 테이블 위의 한 점을 구한다. (받침대 기준 로컬 좌표)
    public Vector3 SpawnPointLocal(float distance, float yawDegrees)
    {
        float yaw = yawDegrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Sin(yaw) * distance,
            spawnHeight,
            Mathf.Cos(yaw) * distance);
    }

    public float NormalizedAngle(int i)
    {
        return joints[i].Angle / 180f;
    } 
    public float NormalizedVelocity(int i)
    {
        return joints[i].Velocity / joints[i].maxSpeed;
    }
    public float reach = 2.7f;
    public Vector3 ToNormalizedLocal(Vector3 worldPosition)
    {
        return ToLocal(worldPosition) / reach;
    }
    
}
